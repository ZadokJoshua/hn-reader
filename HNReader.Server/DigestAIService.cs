using System.Text;
using System.Text.Json;
using HNReader.Server.Configuration;
using HNReader.Server.Models;
using HNReader.Server.Services;
using HNReader.Shared.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace HNReader.Server;

/// <summary>
/// Builds a topical digest of current Hacker News stories using the Microsoft
/// Agent Framework (Chat Completions, OpenAI). Two custom tools — scrape_article
/// and read_comments — let the model actually understand a post instead of
/// working from the title alone, but tool access is scoped to summarization
/// only: classification runs as one tool-free call over the whole ~100-story
/// candidate pool (title + truncated story_text + points/comments/domain is
/// normally enough signal to bucket a topic), while summarization — which
/// operates on a small, bounded per-category shortlist and is what actually
/// reaches the digest output — gets the tools. Giving the whole 100-story
/// classification pass tool access was tried and measured: the model called
/// tools dozens of times sequentially in a single turn, taking 15+ minutes for
/// one run — exactly the "expensive/slow" problem this pipeline exists to
/// avoid, just relocated. Scoping tools to the bounded summarization stage
/// keeps worst-case tool-call volume small while still giving the model real
/// autonomy over when a title needs a tool call to write a good takeaway.
///
/// Candidates come from HN's Algolia search API (genuine recent popularity, not
/// fixed per-story-type quotas). Both the classification and summarization
/// calls use structured JSON output (AIAgent.RunAsync&lt;T&gt;).
///
/// Per-story summaries are cached by story id across runs. HN's popular lists
/// carry the same story for days, so consecutive nightly runs were re-paying for
/// identical work — including the tool fetches, which dominate a run's wall
/// clock. Only stories this pipeline hasn't summarized before reach the model.
/// </summary>
public class DigestAIService
{
    private readonly AlgoliaSearchService _algoliaSearch;
    private readonly ArticleScrapingService _articleScraping;
    private readonly CommentsReadingService _commentsReading;
    private readonly StoryImageService _storyImages;
    private readonly DigestCategoryProvider _categories;
    private readonly DigestStorageService _storage;
    private readonly OpenAIOptions _openAiOptions;
    private readonly DigestOptions _digestOptions;
    private readonly ILogger<DigestAIService> _logger;

    private const int MaxStoryTextChars = 500;

    /// <summary>
    /// The category value the classifier uses for a story that fits none of the
    /// configured categories. An explicit opt-out rather than silence: asked to
    /// simply omit such stories, the model omitted most of the pool.
    /// </summary>
    private const string NoCategory = "None";

    private static readonly JsonSerializerOptions StructuredOutputJsonOptions = new(JsonSerializerDefaults.Web);

    public DigestAIService(
        AlgoliaSearchService algoliaSearch,
        ArticleScrapingService articleScraping,
        CommentsReadingService commentsReading,
        StoryImageService storyImages,
        DigestCategoryProvider categories,
        DigestStorageService storage,
        IOptions<OpenAIOptions> openAiOptions,
        IOptions<DigestOptions> digestOptions,
        ILogger<DigestAIService> logger)
    {
        _algoliaSearch = algoliaSearch;
        _articleScraping = articleScraping;
        _commentsReading = commentsReading;
        _storyImages = storyImages;
        _categories = categories;
        _storage = storage;
        _openAiOptions = openAiOptions.Value;
        _digestOptions = digestOptions.Value;
        _logger = logger;
    }

    public async Task<DigestDto> GenerateDailyDigestAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_openAiOptions.ApiKey))
        {
            throw new InvalidOperationException(
                "OpenAI:ApiKey is not configured. Set it via user secrets or the OpenAI__ApiKey environment variable.");
        }

        // The taxonomy is read ONCE for the whole run, into a local, and threaded
        // through every stage as a parameter. Re-reading it per stage would let a
        // category added mid-run be summarized having never been offered to the
        // classifier — a guaranteed zero-story section.
        var categoryNames = _categories.GetSnapshot().EnabledNames;
        if (categoryNames.Count == 0)
        {
            throw new InvalidOperationException(
                "No digest categories are enabled; aborting rather than persisting an empty digest.");
        }

        // Prune before reading: an entry past its TTL should be re-summarized by
        // this run, not served from cache and then pruned after the fact.
        var pruned = await _storage
            .PruneCachedSummariesAsync(TimeSpan.FromDays(Math.Max(1, _digestOptions.StorySummaryCacheDays)))
            .ConfigureAwait(false);
        if (pruned > 0)
        {
            _logger.LogInformation("Digest: pruned {Count} expired story summaries from the cache", pruned);
        }

        var candidates = await _algoliaSearch.GetPopularStoriesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Digest: fetched {Count} candidate stories from Algolia", candidates.Count);

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("Algolia returned no candidate stories; aborting digest run.");
        }

        var client = new OpenAIClient(_openAiOptions.ApiKey);
        var classificationAgent = CreateClassificationAgent(client);

        var classifications = await ClassifyAsync(
            classificationAgent, candidates, categoryNames, cancellationToken).ConfigureAwait(false);

        // Stage the classified pool ("arrange the stories") before summarization
        // starts reading them back grouped by category.
        var staged = BuildStagedCandidates(candidates, classifications, categoryNames);
        await _storage.SaveStagedCandidatesAsync(staged).ConfigureAwait(false);

        // Categories are independent — one LLM call plus a couple of tool fetches
        // each — so they run concurrently rather than end-to-end (a sequential run
        // was measured at ~10.5 minutes). Concurrency stays bounded because the
        // tools hit news.ycombinator.com, which times out under heavier load.
        var maxParallel = Math.Max(1, _digestOptions.MaxParallelCategories);
        using var throttle = new SemaphoreSlim(maxParallel, maxParallel);

        var tasks = categoryNames.Select(async categoryName =>
        {
            var stagedForCategory = await _storage.GetStagedCandidatesAsync(categoryName).ConfigureAwait(false);

            // No stories classified into this category today — skip it entirely
            // rather than spending an LLM call to summarize nothing. This is what
            // makes a wide, specific taxonomy free: unused buckets cost nothing.
            if (stagedForCategory.Count == 0) return CategoryResult.Skipped;

            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // A fresh agent (with fresh tool-call counters) per category — the
                // prompt-level "use tools sparingly" instruction alone wasn't
                // reliable (measured: 87+ tool calls in one run despite asking for
                // at most 2 per category), so the cap is enforced in code instead.
                var summarizationAgent = CreateSummarizationAgent(client, cancellationToken);
                return await SummarizeCategoryAsync(
                    summarizationAgent, categoryName, stagedForCategory, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                throttle.Release();
            }
        });

        // Task.WhenAll preserves input order, so the digest still comes back in the
        // stored taxonomy order regardless of which category finished first.
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        var produced = results.Where(r => r.Category is not null).ToList();
        var categories = produced.Select(r => r.Category!).ToList();
        var failureCount = produced.Count(r => r.Failed);
        var reusedCount = produced.Sum(r => r.ReusedFromCache);

        if (categories.Count == 0)
        {
            throw new InvalidOperationException(
                "No categories were produced for this digest run; aborting rather than persisting an empty digest.");
        }

        // A run whose summarization calls all failed still returns a structurally
        // valid digest — titles and links with blank prose. Persisting that would
        // replace the last good digest with an empty-looking one, so fail the run
        // and let Hangfire retry instead.
        if (failureCount == categories.Count)
        {
            throw new InvalidOperationException(
                $"All {failureCount} category summarization calls failed; aborting rather than persisting a blank digest.");
        }

        if (failureCount > 0)
        {
            _logger.LogWarning(
                "Digest: {FailureCount} of {Total} categories failed to summarize and will have empty prose",
                failureCount, categories.Count);
        }

        var cacheEntriesToPersist = produced.SelectMany(r => r.CacheEntriesToPersist).ToList();

        // Preview images are enrichment, not content: this runs after the digest
        // is otherwise complete so a slow or unreachable page can only cost the
        // run a little time, never an item.
        categories = await EnrichWithPreviewImagesAsync(categories, cacheEntriesToPersist, cancellationToken)
            .ConfigureAwait(false);

        await _storage.SaveCachedSummariesAsync(cacheEntriesToPersist).ConfigureAwait(false);
        await _storage.ClearStagedCandidatesAsync().ConfigureAwait(false);

        _logger.LogInformation(
            "Digest: produced {CategoryCount} categories covering {ItemCount} stories " +
            "({ReusedCount} summaries reused from cache, {NewCount} newly summarized)",
            categories.Count, categories.Sum(c => c.Items.Count()), reusedCount,
            produced.Sum(r => r.NewlySummarized));

        return new DigestDto(DateTime.UtcNow, categories);
    }

    /// <summary>
    /// Outcome of one category's summarization: the category itself (null when the
    /// category had no stories today), whether its LLM call failed, and how many
    /// of its stories came from the summary cache.
    /// <para>
    /// <see cref="CacheEntriesToPersist"/> is every cache row this category needs
    /// written back — the summaries this run newly produced, plus any served from
    /// cache that still have no recorded preview-image attempt. Those two cases
    /// travel together because they need the same thing: an image lookup, then a
    /// write. Limiting the list to only *new* summaries meant a story cached
    /// without an image never got its attempt recorded, so every future run
    /// re-fetched the same image-less page indefinitely.
    /// </para>
    /// </summary>
    private sealed record CategoryResult(
        DigestCategoryDto? Category,
        bool Failed,
        int ReusedFromCache,
        int NewlySummarized,
        List<CachedStorySummary> CacheEntriesToPersist)
    {
        public static readonly CategoryResult Skipped = new(null, false, 0, 0, []);
    }

    /// <summary>
    /// Tool-free and cheap — runs once over the whole ~100-story candidate pool.
    /// Title + truncated story_text + points/comments/domain is normally enough
    /// to bucket a topic; giving this pass tool access invited the model to call
    /// tools per-story across the whole batch, which is what made an earlier
    /// version of this pipeline slow (15+ minutes for one run).
    /// </summary>
    private AIAgent CreateClassificationAgent(OpenAIClient client)
    {
        var chatClient = client.GetChatClient(_openAiOptions.Model);

        return chatClient.AsAIAgent(
            instructions: "You classify Hacker News stories into a fixed set of specific topical categories for a " +
                          "daily digest, using each story's title, any provided text, points, comment count, and " +
                          "domain. There is no catch-all category: pick the single best fit, and only decline to " +
                          "classify a story if it genuinely matches none of them. " +
                          "Always respond with the exact JSON shape requested, and nothing else.",
            name: "hn-digest-classifier");
    }

    /// <summary>
    /// Tool-equipped — but only ever runs over a small, bounded per-category
    /// shortlist (<see cref="DigestOptions.MaxItemsPerCategory"/> stories), and
    /// each tool is wrapped with a call counter capped by the configured
    /// per-category limits so a model that ignores the "use sparingly"
    /// instruction can't blow up the conversation's context window or turn one
    /// category into a multi-minute chain of tool round-trips.
    /// </summary>
    private AIAgent CreateSummarizationAgent(OpenAIClient client, CancellationToken cancellationToken)
    {
        var chatClient = client.GetChatClient(_openAiOptions.Model);

        var maxScrapes = Math.Max(0, _digestOptions.MaxScrapeCallsPerCategory);
        var maxComments = Math.Max(0, _digestOptions.MaxCommentCallsPerCategory);

        var scrapeCalls = 0;
        var commentCalls = 0;

        AITool[] tools =
        [
            AIFunctionFactory.Create(async (string url) =>
            {
                if (Interlocked.Increment(ref scrapeCalls) > maxScrapes)
                {
                    return "Tool call limit reached for this category — summarize this story from its title instead.";
                }
                return await _articleScraping.ScrapeArticleAsync(url, cancellationToken).ConfigureAwait(false);
            }, "scrape_article",
                "Fetches and extracts the main readable text of a web page given its URL. Use this when a " +
                "story's title isn't descriptive enough to write a good one-line takeaway. Limited to " +
                $"{maxScrapes} calls per category — use it only for the stories that most need it."),
            AIFunctionFactory.Create(async (int storyId) =>
            {
                if (Interlocked.Increment(ref commentCalls) > maxComments)
                {
                    return "Tool call limit reached for this category — proceed without checking comments for this story.";
                }
                return await _commentsReading.ReadCommentsAsync(storyId, cancellationToken).ConfigureAwait(false);
            }, "read_comments",
                "Fetches the top-level comments for a Hacker News story by its id, giving community context and " +
                "reactions. Use this when a story's discussion looks substantial enough to be worth reporting on " +
                $"in detail. Limited to {maxComments} calls per category — use it on the stories most likely to " +
                "have notable discussion."),
        ];

        return chatClient.AsAIAgent(
            instructions: "You write digest sections summarizing Hacker News stories. Be concise and factual in " +
                          "story takeaways — do not invent details. Use scrape_article only when a story's title " +
                          "alone isn't enough to write a good takeaway. Use read_comments on the stories with the " +
                          "most substantial discussion, and when you do, report on that discussion in real detail " +
                          "rather than in one vague line. Respect the per-category tool limits given in the tool " +
                          "descriptions. Always respond with the exact JSON shape requested, and nothing else.",
            name: "hn-digest-summarizer",
            tools: tools);
    }

    /// <summary>
    /// Classifies the candidate pool in concurrent batches rather than one call.
    /// A single 100-story call is where this pass silently broke: the response ran
    /// into the model's output limit and came back with roughly a quarter of the
    /// ids, so most of the pool was dropped with no error anywhere — the run just
    /// produced a thin digest. Batches keep each response short enough to come
    /// back whole, and a batch that fails costs only its own stories instead of
    /// the run.
    /// </summary>
    private async Task<List<StoryClassification>> ClassifyAsync(
        AIAgent agent,
        List<HNSearchHit> candidates,
        IReadOnlyList<string> categoryNames,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0) return [];

        var batchSize = Math.Max(1, _digestOptions.ClassificationBatchSize);
        var batches = candidates
            .Select((story, index) => (story, index))
            .GroupBy(x => x.index / batchSize)
            .Select(g => g.Select(x => x.story).ToList())
            .ToList();

        var results = await Task.WhenAll(batches.Select(batch =>
            ClassifyBatchAsync(agent, batch, categoryNames, cancellationToken))).ConfigureAwait(false);

        var classifications = results.SelectMany(r => r).ToList();

        // Every batch failing means the whole pass produced nothing: nothing gets
        // staged, every category is skipped, and the run would otherwise persist
        // an empty digest over a good one.
        if (classifications.Count == 0)
        {
            throw new InvalidOperationException(
                "Digest classification produced no results across all batches; aborting run.");
        }

        _logger.LogInformation(
            "Digest: classified {Classified} of {Total} candidate stories across {Batches} batches",
            classifications.Count, candidates.Count, batches.Count);

        return classifications;
    }

    private async Task<List<StoryClassification>> ClassifyBatchAsync(
        AIAgent agent,
        List<HNSearchHit> batch,
        IReadOnlyList<string> categoryNames,
        CancellationToken cancellationToken)
    {
        var prompt = BuildClassificationPrompt(batch, categoryNames);

        try
        {
            var response = await agent.RunAsync<ClassificationResponse>(
                prompt, serializerOptions: StructuredOutputJsonOptions, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var classifications = response.Result?.Classifications ?? [];

            if (classifications.Count < batch.Count)
            {
                // Not fatal — the stories that did come back are still usable — but
                // it is the signature of a truncated response, and worth seeing in
                // the log rather than only as a thinner digest.
                _logger.LogWarning(
                    "Digest: classification batch returned {Returned} of {Expected} stories",
                    classifications.Count, batch.Count);
            }

            return classifications;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Digest: classification batch of {Count} stories failed", batch.Count);
            return [];
        }
    }

    private static string BuildClassificationPrompt(
        List<HNSearchHit> candidates, IReadOnlyList<string> categoryNames)
    {
        var categoryList = string.Join(", ", categoryNames.Select(c => $"\"{c}\""));

        var sb = new StringBuilder();
        sb.AppendLine("Classify each of the following Hacker News stories into exactly one of these categories:");
        sb.AppendLine(categoryList);
        sb.AppendLine("Every category is specific and there is no catch-all bucket, so choose the single closest " +
                      $"fit. If a story genuinely fits none of them, give it the category \"{NoCategory}\" and it " +
                      "will be left out of the digest.");
        sb.AppendLine("Stories:");
        foreach (var story in candidates)
        {
            var domain = TryGetDomain(story.Url);
            sb.Append($"- id={story.ObjectId}, title=\"{story.Title}\", points={story.Points ?? 0}, " +
                      $"comments={story.NumComments ?? 0}, domain={domain}");

            if (!string.IsNullOrWhiteSpace(story.StoryText))
            {
                var text = story.StoryText;
                var truncated = text.Length > MaxStoryTextChars ? text[..MaxStoryTextChars] + "… [truncated]" : text;
                sb.Append($", text=\"{truncated}\"");
            }

            sb.AppendLine();
        }
        // "one entry per story you were able to classify" was read as licence to
        // return a short list; the count has to be stated explicitly.
        sb.AppendLine("Respond with JSON: { \"classifications\": [ { \"id\": <story id>, \"category\": \"<category>\" }, ... ] } " +
                      $"containing exactly {candidates.Count} entries — one for every story id listed above, in the " +
                      "same order. Do not omit any id.");
        return sb.ToString();
    }

    /// <summary>
    /// Joins the classifier's category-per-id back onto the real story data.
    /// A story whose category isn't in the configured taxonomy is dropped rather
    /// than swept into a fallback bucket: the taxonomy deliberately has no
    /// catch-all, and inventing a home for an unclassifiable story is how the old
    /// "Other / General Tech" section ended up a bag of unrelated links.
    /// <paramref name="categoryNames"/> is the run's single taxonomy snapshot —
    /// see GenerateDailyDigestAsync.
    /// </summary>
    private List<StagedCandidate> BuildStagedCandidates(
        List<HNSearchHit> candidates,
        List<StoryClassification> classifications,
        IReadOnlyList<string> categoryNames)
    {
        var storiesById = candidates
            .Where(s => int.TryParse(s.ObjectId, out _))
            .ToDictionary(s => int.Parse(s.ObjectId));
        var categoryByStoryId = new Dictionary<int, string>();
        var deliberatelyUnclassified = 0;
        var unrecognizedCategory = 0;

        foreach (var classification in classifications)
        {
            if (!storiesById.ContainsKey(classification.Id)) continue;

            if (NoCategory.Equals(classification.Category, StringComparison.OrdinalIgnoreCase))
            {
                deliberatelyUnclassified++;
                continue;
            }

            // Match case-insensitively but store the taxonomy's own spelling, so a
            // digest's category names always match what /digest whitelists.
            var matched = categoryNames
                .FirstOrDefault(c => c.Equals(classification.Category, StringComparison.OrdinalIgnoreCase));

            if (matched is null)
            {
                unrecognizedCategory++;
                continue;
            }

            categoryByStoryId[classification.Id] = matched;
        }

        var dropped = storiesById.Count - categoryByStoryId.Count;
        if (dropped > 0)
        {
            // Split three ways because they mean different things: "fits nothing"
            // is the taxonomy working as intended, an unrecognized name is the
            // model inventing a category, and a story never mentioned at all means
            // the classification response came back incomplete.
            _logger.LogInformation(
                "Digest: {Dropped} of {Total} candidate stories left out — {NoFit} fit no category, " +
                "{Unrecognized} given a category that does not exist, {Missing} never classified",
                dropped, storiesById.Count, deliberatelyUnclassified, unrecognizedCategory,
                dropped - deliberatelyUnclassified - unrecognizedCategory);
        }

        return categoryByStoryId
            .Select(entry => (Story: storiesById[entry.Key], Category: entry.Value))
            .Select(x => new StagedCandidate
            {
                StoryId = int.Parse(x.Story.ObjectId),
                Title = x.Story.Title ?? string.Empty,
                Url = x.Story.Url,
                Score = x.Story.Points ?? 0,
                CommentCount = x.Story.NumComments ?? 0,
                RootDomain = TryGetDomain(x.Story.Url),
                Author = x.Story.Author,
                Category = x.Category
            })
            .ToList();
    }

    /// <summary>
    /// Summarizes one category, reusing any per-story summaries already in the
    /// cache. The LLM is still called once for the category overview (which
    /// depends on today's particular mix of stories, so it can't be cached), but
    /// it's only asked to write takeaways for stories not already summarized —
    /// which is also what keeps the scrape/comment tool budget aimed at genuinely
    /// new material instead of re-reading yesterday's threads.
    /// </summary>
    private async Task<CategoryResult> SummarizeCategoryAsync(
        AIAgent agent, string categoryName, List<StagedCandidate> staged, CancellationToken cancellationToken)
    {
        var topStories = staged
            .OrderByDescending(s => s.Score)
            .Take(_digestOptions.MaxItemsPerCategory)
            .ToList();

        var cached = await _storage
            .GetCachedSummariesAsync(topStories.Select(s => s.StoryId).ToList())
            .ConfigureAwait(false);

        var needSummary = topStories.Where(s => !cached.ContainsKey(s.StoryId)).ToList();

        var prompt = BuildSummaryPrompt(categoryName, topStories, cached, needSummary);

        var summary = await RunSummarizationWithRetryAsync(agent, prompt, categoryName, cancellationToken)
            .ConfigureAwait(false);
        var failed = summary is null;

        var takeawaysById = (summary?.Items ?? []).ToDictionary(i => i.Id, i => i);
        var toPersist = new List<CachedStorySummary>();
        var newlySummarized = 0;

        var items = topStories.Select(story =>
        {
            var storySummary = string.Empty;
            string? commentNote = null;
            string? imageUrl = null;

            if (cached.TryGetValue(story.StoryId, out var cachedSummary))
            {
                storySummary = cachedSummary.Summary;
                commentNote = cachedSummary.CommentNote;
                imageUrl = cachedSummary.ImageUrl;

                // Prose is reused as-is, but a cache row that never had its image
                // looked up (written before preview images existed, or by a run
                // where the fetch was disabled) is queued for one attempt.
                if (!cachedSummary.ImageFetchAttempted)
                {
                    toPersist.Add(cachedSummary);
                }
            }
            else if (takeawaysById.TryGetValue(story.StoryId, out var takeaway))
            {
                storySummary = takeaway.Takeaway;
                commentNote = takeaway.CommentNote;
                newlySummarized++;

                toPersist.Add(new CachedStorySummary
                {
                    StoryId = story.StoryId,
                    Title = story.Title,
                    Summary = takeaway.Takeaway,
                    CommentNote = takeaway.CommentNote,
                    CachedAtUtc = DateTime.UtcNow
                });
            }

            return new DigestItemDto(
                StoryId: story.StoryId,
                Title: story.Title,
                Url: story.Url,
                HackerNewsUrl: $"https://news.ycombinator.com/item?id={story.StoryId}",
                Summary: storySummary,
                Author: story.Author ?? string.Empty,
                CommentNote: commentNote,
                ImageUrl: imageUrl);
        }).ToList();

        var category = new DigestCategoryDto(categoryName, summary?.Summary ?? string.Empty, items);

        return new CategoryResult(
            category, failed, topStories.Count - needSummary.Count, newlySummarized, toPersist);
    }

    /// <summary>
    /// Runs one category's summarization call, retrying transient API failures.
    /// Both failure modes that actually showed up in testing are transient and
    /// hit several categories of a single run at once: an OpenAI 429 when the
    /// run's concurrent calls cross the organization's tokens-per-minute limit,
    /// and plain network failure (a DNS outage mid-run took out five categories).
    /// Without a retry each one silently costs a whole section its prose, which
    /// is a poor trade against waiting a few seconds.
    /// </summary>
    private async Task<CategorySummaryResponse?> RunSummarizationWithRetryAsync(
        AIAgent agent, string prompt, string categoryName, CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await agent.RunAsync<CategorySummaryResponse>(
                    prompt, serializerOptions: StructuredOutputJsonOptions, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return response.Result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (attempt == maxAttempts || !IsTransient(ex))
                {
                    _logger.LogError(
                        ex, "Digest: summarization call failed for category {Category} after {Attempts} attempt(s)",
                        categoryName, attempt);
                    return null;
                }

                // Backs off rather than retrying immediately: a 429 means the
                // whole run is over the per-minute token budget, so an instant
                // retry just spends another slice of it.
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt) * 2);
                _logger.LogWarning(
                    "Digest: transient summarization failure for category {Category} " +
                    "(attempt {Attempt}/{MaxAttempts}); retrying in {Delay}s — {Message}",
                    categoryName, attempt, maxAttempts, delay.TotalSeconds, ex.Message);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        return null;
    }

    /// <summary>
    /// Whether an exception is worth another attempt: rate limits, server-side
    /// errors, and transport failures are; a malformed request or a bad API key
    /// will fail identically however many times it is retried.
    /// </summary>
    private static bool IsTransient(Exception exception)
    {
        foreach (var ex in Flatten(exception))
        {
            if (ex is System.ClientModel.ClientResultException clientEx)
            {
                if (clientEx.Status is 408 or 429 or >= 500) return true;
                continue;
            }

            if (ex is HttpRequestException or System.Net.Sockets.SocketException or TimeoutException) return true;
        }

        return false;
    }

    private static IEnumerable<Exception> Flatten(Exception exception)
    {
        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.Flatten().InnerExceptions)
            {
                foreach (var nested in Flatten(inner)) yield return nested;
            }

            yield break;
        }

        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    private static string BuildSummaryPrompt(
        string categoryName,
        List<StagedCandidate> stories,
        Dictionary<int, CachedStorySummary> cached,
        List<StagedCandidate> needSummary)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Write a digest section for the \"{categoryName}\" category based on these stories:");
        foreach (var story in stories)
        {
            sb.Append($"- id={story.StoryId}, title=\"{story.Title}\", url={story.Url ?? "(no link - HN text post)"}, " +
                      $"score={story.Score}, comments={story.CommentCount}, domain={story.RootDomain}");

            // Already-summarized stories still get listed, because the overview
            // has to describe the whole section — but showing their existing
            // takeaway stops the model re-deriving (or worse, re-fetching) them.
            if (cached.TryGetValue(story.StoryId, out var existing))
            {
                sb.Append(" [ALREADY SUMMARIZED - do not write a takeaway or call any tool for this one. " +
                          $"Its existing takeaway, for the overview's context only: \"{existing.Summary}\"]");
            }

            sb.AppendLine();
        }

        if (needSummary.Count == 0)
        {
            sb.AppendLine("Every story above has already been summarized, so write ONLY the category overview. " +
                          "Do not call any tools. Return an empty items array.");
            sb.AppendLine("Respond with JSON: { \"summary\": \"<2-4 sentence overview of this category today>\", " +
                          "\"items\": [] }");
            return sb.ToString();
        }

        var idsNeeded = string.Join(", ", needSummary.Select(s => s.StoryId));
        sb.AppendLine($"Write a takeaway ONLY for these story ids: {idsNeeded}. Treat the others purely as context " +
                      "for the overview.");
        sb.AppendLine("If a story's title doesn't tell you enough to write a good takeaway, use scrape_article to " +
                      "open its url first; otherwise don't bother. Never call a tool for a story marked ALREADY " +
                      "SUMMARIZED.");
        sb.AppendLine("For the stories with the most substantial discussion, use read_comments and then write a " +
                      "DETAILED commentNote of at least four sentences. A good note covers: the main lines of " +
                      "argument and who takes which side; specific claims, corrections, or firsthand experience " +
                      "commenters brought; any notable disagreement, skepticism, or consensus; and anything the " +
                      "discussion raised that the story itself didn't address. Paraphrase concrete points rather " +
                      "than characterizing the mood in the abstract — \"opinions were mixed\" is not a useful " +
                      "note. Set commentNote to null for any story whose comments you did not actually read; " +
                      "never guess at a discussion you haven't seen.");
        sb.AppendLine("Respond with JSON: { \"summary\": \"<2-4 sentence overview of this category today>\", " +
                      "\"items\": [ { \"id\": <story id>, \"takeaway\": \"<one-line takeaway>\", " +
                      "\"commentNote\": \"<detailed multi-sentence reaction note, or null>\" }, ... ] } " +
                      $"with one items entry for each of these ids: {idsNeeded}.");
        return sb.ToString();
    }

    /// <summary>
    /// Attaches a preview image to each item whose story links out to a real
    /// article, for client-side thumbnails. Entirely optional: a story with no
    /// findable image keeps a null ImageUrl, and the attempt is recorded on the
    /// cache entry so an image-less page isn't re-fetched by every future run.
    /// Runs concurrently but bounded — this is dozens of requests to unrelated
    /// third-party sites and must not become the slowest part of a run.
    /// </summary>
    private async Task<List<DigestCategoryDto>> EnrichWithPreviewImagesAsync(
        List<DigestCategoryDto> categories,
        List<CachedStorySummary> cacheEntriesToPersist,
        CancellationToken cancellationToken)
    {
        if (!_digestOptions.EnablePreviewImages) return categories;

        // Driven off the cache rows being written rather than off "items with no
        // image": those rows are exactly the stories with no recorded lookup yet,
        // so a page that genuinely has no image is asked once and then left alone
        // by every future run.
        var cacheByStoryId = cacheEntriesToPersist
            .GroupBy(s => s.StoryId)
            .ToDictionary(g => g.Key, g => g.First());

        // Self-posts have no page of their own to read an image from.
        var urlByStoryId = categories
            .SelectMany(c => c.Items)
            .Where(i => cacheByStoryId.ContainsKey(i.StoryId) && !string.IsNullOrWhiteSpace(i.Url))
            .GroupBy(i => i.StoryId)
            .ToDictionary(g => g.Key, g => g.First().Url);

        if (urlByStoryId.Count == 0) return categories;

        var maxParallel = Math.Max(1, _digestOptions.MaxParallelImageFetches);
        using var throttle = new SemaphoreSlim(maxParallel, maxParallel);

        var lookups = urlByStoryId.Select(async entry =>
        {
            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var result = await _storyImages
                    .TryGetPreviewImageAsync(entry.Value, cancellationToken)
                    .ConfigureAwait(false);
                return (StoryId: entry.Key, Result: result);
            }
            finally
            {
                throttle.Release();
            }
        });

        var resolved = await Task.WhenAll(lookups).ConfigureAwait(false);
        var imageByStoryId = resolved
            .Where(r => r.Result.ImageUrl is not null)
            .ToDictionary(r => r.StoryId, r => r.Result.ImageUrl!);

        // Only a conclusive lookup is worth remembering. Recording an
        // unreachable page as "attempted" would retire the story from all future
        // lookups on the strength of one bad network moment.
        foreach (var (storyId, result) in resolved)
        {
            if (!cacheByStoryId.TryGetValue(storyId, out var entry)) continue;
            if (!result.Conclusive) continue;

            entry.ImageUrl = result.ImageUrl;
            entry.ImageFetchAttempted = true;
        }

        // A self-post has no page to read, which is conclusive — mark it, or it
        // stays queued forever for a lookup that cannot succeed.
        foreach (var entry in cacheEntriesToPersist.Where(e => !urlByStoryId.ContainsKey(e.StoryId)))
        {
            entry.ImageFetchAttempted = true;
        }

        var unreachable = resolved.Count(r => !r.Result.Conclusive);
        _logger.LogInformation(
            "Digest: preview images found for {Found} of {Attempted} linked stories " +
            "({Unreachable} pages unreachable, will be retried next run)",
            imageByStoryId.Count, urlByStoryId.Count, unreachable);

        return categories
            .Select(c => c with
            {
                Items = c.Items
                    .Select(i => imageByStoryId.TryGetValue(i.StoryId, out var image) ? i with { ImageUrl = image } : i)
                    .ToList()
            })
            .ToList();
    }

    private static string TryGetDomain(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "self";

        try
        {
            var host = new Uri(url).Host;
            var parts = host.Split('.');
            return parts.Length >= 2 ? string.Join('.', parts[^2..]) : host;
        }
        catch
        {
            return "self";
        }
    }
}
