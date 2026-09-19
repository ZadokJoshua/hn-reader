using System.Text;
using HNReader.Core.Models;
using HNReader.Core.Services;
using HNReader.Server.Configuration;
using Microsoft.Extensions.Options;

namespace HNReader.Server.Services;

/// <summary>
/// Backs the digest agent's "read_comments" tool, reusing the already-tested
/// <see cref="HNWebClient"/> the WinUI/Avalonia apps use for comment threads.
/// Never throws — a tool result the agent can always use is more useful than a
/// failed tool call it has to reason about.
///
/// Retries once on a transient fetch failure: a measured digest run saw 7 of 18
/// read_comments calls time out against news.ycombinator.com, which silently
/// stripped the community-reaction notes from a third of the digest. One cheap
/// retry recovers most of those without turning the tool into a slow spin.
///
/// <para>
/// Fetches are also globally throttled. Per-category tool limits bound how often
/// any one category asks for comments, but they multiply by however many
/// categories are summarized in parallel — a run with 12 categories pushed 50
/// requests at HN and 28% of them timed out (measured). One instance of this
/// service is shared by every category task in a run, so the semaphore here is
/// the whole run's budget: HN is the scarce resource, and it is scarce per-host,
/// not per-category.
/// </para>
/// </summary>
public class CommentsReadingService
{
    // Raised from 6/1000: the digest now asks for a detailed, multi-sentence
    // note on a story's discussion — specific claims, who disagreed with whom,
    // what the article missed — and six comments truncated to 1000 characters
    // total is not enough material to write that from without padding. Still
    // bounded, because this text goes into the summarization prompt's context.
    private const int MaxTopLevelComments = 12;
    private const int MaxTotalChars = 4000;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    private readonly HNWebClient _webClient;
    private readonly SemaphoreSlim _fetchThrottle;
    private readonly ILogger<CommentsReadingService> _logger;

    public CommentsReadingService(
        HNWebClient webClient,
        IOptions<DigestOptions> digestOptions,
        ILogger<CommentsReadingService> logger)
    {
        _webClient = webClient;
        _logger = logger;

        var maxConcurrent = Math.Max(1, digestOptions.Value.MaxConcurrentCommentFetches);
        _fetchThrottle = new SemaphoreSlim(maxConcurrent, maxConcurrent);
    }

    /// <summary>Fetches the top-level comments for an HN story to gauge community reaction.</summary>
    /// <param name="storyId">The HN story id.</param>
    public async Task<string> ReadCommentsAsync(int storyId, CancellationToken cancellationToken = default)
    {
        try
        {
            var comments = await FetchWithRetryAsync(storyId, cancellationToken).ConfigureAwait(false);
            var topLevel = comments.Where(c => c.Depth == 0).Take(MaxTopLevelComments).ToList();

            if (topLevel.Count == 0)
            {
                return "No comments available for this story.";
            }

            var sb = new StringBuilder();
            foreach (var comment in topLevel)
            {
                if (sb.Length >= MaxTotalChars) break;

                var text = comment.Text ?? string.Empty;
                var remaining = MaxTotalChars - sb.Length;
                if (text.Length > remaining) text = text[..remaining];

                sb.AppendLine($"{comment.By}: {text}");
            }

            var result = sb.ToString().Trim();
            return result.Length >= MaxTotalChars ? result + "… [truncated]" : result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Digest: read_comments failed for story {StoryId}", storyId);
            return "Comments unavailable: failed to fetch this story's discussion.";
        }
    }

    private async Task<List<WebComment>> FetchWithRetryAsync(int storyId, CancellationToken cancellationToken)
    {
        await _fetchThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await FetchWithRetryCoreAsync(storyId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _fetchThrottle.Release();
        }
    }

    private async Task<List<WebComment>> FetchWithRetryCoreAsync(int storyId, CancellationToken cancellationToken)
    {
        try
        {
            return await _webClient.GetCommentsFromWebAsync(storyId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Digest: read_comments attempt 1 failed for story {StoryId}; retrying", storyId);
            await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
            return await _webClient.GetCommentsFromWebAsync(storyId, cancellationToken).ConfigureAwait(false);
        }
    }
}
