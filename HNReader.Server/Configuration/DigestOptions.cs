namespace HNReader.Server.Configuration;

/// <summary>
/// Bound from the "Digest" configuration section — the digest pipeline's tuning
/// knobs. The topical taxonomy itself deliberately lives in the database rather
/// than here (see DigestCategoryProvider and DigestCategorySeed): a list that
/// can only change by editing this file and redeploying isn't manageable, and
/// binding a List&lt;string&gt; from configuration is what caused the 16-to-32
/// category duplication bug this class used to need a Clear() workaround for.
/// </summary>
public class DigestOptions
{
    /// <summary>How far back the Algolia popularity search looks for candidate stories.</summary>
    public int AlgoliaLookbackHours { get; set; } = 24;

    /// <summary>Optional minimum points filter on the Algolia search; 0 disables the filter.</summary>
    public int MinPoints { get; set; } = 0;

    /// <summary>
    /// Hard cap on how many candidate stories get sent to the classifier in one
    /// prompt — also used as the Algolia search's hitsPerPage.
    /// </summary>
    public int MaxCandidatePoolSize { get; set; } = 100;

    /// <summary>
    /// How many stories go into one classification call. The whole pool used to
    /// go in a single call, and it silently stopped scaling: with a wider
    /// taxonomy (longer category names) a 100-story request ran into the model's
    /// output limit and came back with only ~24 of the 100 ids classified, so
    /// three quarters of the digest quietly vanished. Batching keeps each
    /// response comfortably short; the batches are independent, so they run
    /// concurrently and cost no extra wall clock.
    /// </summary>
    public int ClassificationBatchSize { get; set; } = 25;

    /// <summary>Max items summarized per category in the digest output.</summary>
    public int MaxItemsPerCategory { get; set; } = 8;

    /// <summary>
    /// How many categories are summarized concurrently. Each category is one LLM
    /// call plus up to a handful of tool fetches, and they're fully independent,
    /// so running them strictly one-at-a-time made a full digest take ~10 minutes
    /// (measured). Kept deliberately low rather than unbounded: the tools hit
    /// news.ycombinator.com, which starts timing out when hammered.
    /// </summary>
    public int MaxParallelCategories { get; set; } = 3;

    /// <summary>
    /// Per-category ceiling on scrape_article calls, enforced in code — the
    /// prompt-level "use sparingly" instruction alone wasn't reliable (measured:
    /// 87+ tool calls in one run despite asking for at most 2 per category).
    /// </summary>
    public int MaxScrapeCallsPerCategory { get; set; } = 2;

    /// <summary>
    /// Per-category ceiling on read_comments calls. Higher than the scrape limit
    /// because comment notes are a headline feature of the digest and story
    /// summaries are now cached across runs, so a run's tool budget is spent
    /// only on stories it hasn't seen before.
    /// </summary>
    public int MaxCommentCallsPerCategory { get; set; } = 3;

    /// <summary>
    /// Ceiling on comment fetches in flight across the whole run, independent of
    /// how many categories are being summarized at once. news.ycombinator.com is
    /// the bottleneck here, not us: with per-category limits alone, a run with
    /// many categories drove 28% of comment fetches into timeouts (measured),
    /// because the per-category caps multiply by the category concurrency. This
    /// caps the product.
    /// </summary>
    public int MaxConcurrentCommentFetches { get; set; } = 2;

    /// <summary>
    /// How long a per-story summary stays reusable. HN's popular lists carry the
    /// same story for several days, and a story's own summary doesn't change once
    /// written, so re-running the LLM (and its tool fetches) over it every night
    /// is pure waste. Entries older than this are pruned and re-summarized, so a
    /// stale or poor summary can't stick around forever.
    /// </summary>
    public int StorySummaryCacheDays { get; set; } = 14;

    /// <summary>
    /// Whether to enrich items with a preview image scraped from the story's own
    /// page. Purely cosmetic for clients — see <see cref="StoryImageService"/>.
    /// </summary>
    public bool EnablePreviewImages { get; set; } = true;

    /// <summary>How many preview-image fetches run concurrently.</summary>
    public int MaxParallelImageFetches { get; set; } = 8;

    /// <summary>Standard cron expression; default is midnight UTC daily.</summary>
    public string CronSchedule { get; set; } = "0 0 * * *";
}
