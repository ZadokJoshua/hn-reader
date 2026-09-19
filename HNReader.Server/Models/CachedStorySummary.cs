using LiteDB;

namespace HNReader.Server.Models;

/// <summary>
/// A per-story summary retained across digest runs, keyed by HN story id.
/// <para>
/// HN's popular lists carry the same story for several days, so consecutive
/// nightly runs kept re-summarizing stories they had already summarized —
/// paying for the LLM output and, worse, the scrape_article/read_comments
/// fetches, which are the slow part of a run. A story's own summary doesn't
/// change once written, so it's cached here and reused; only genuinely new
/// stories reach the model.
/// </para>
/// <para>
/// <see cref="ImageUrl"/> is cached alongside the prose because it comes from
/// the same place conceptually — a fetch against the story's own page — and is
/// equally stable. <see cref="ImageFetchAttempted"/> distinguishes "we looked
/// and the page has no usable image" from "we haven't looked yet", so a
/// repeatedly image-less story isn't re-fetched every night.
/// </para>
/// Plain mutable class (not a record) so LiteDB's BSON mapper has no ambiguity
/// about which constructor to use.
/// </summary>
public class CachedStorySummary
{
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? CommentNote { get; set; }
    public string? ImageUrl { get; set; }
    public bool ImageFetchAttempted { get; set; }
    public DateTime CachedAtUtc { get; set; }
}
