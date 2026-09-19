namespace HNReader.Shared.Models;

public record DigestDto(
    DateTime GeneratedAtUtc,
    IEnumerable<DigestCategoryDto> Categories);

/// <summary>
/// One topical section of the digest. Carries only an overview plus its items:
/// an earlier version also had a category-level CommentSentiment, but with
/// per-story comment notes now detailed it only restated them one altitude up.
/// </summary>
public record DigestCategoryDto(
    string Name,
    string Summary,
    IEnumerable<DigestItemDto> Items);

/// <summary>
/// One story in the digest.
/// <para>
/// <see cref="Url"/> is the story's own external link and is null for HN
/// self-posts (Ask HN, Show HN without a link, text posts) — clients should fall
/// back to <see cref="HackerNewsUrl"/>, which is always present and always
/// points at the HN discussion page. An earlier version collapsed the two into a
/// single Url that silently held the HN link for self-posts, which left clients
/// unable to tell "read the article" from "read the thread", and unable to offer
/// both for the common case where a story has an article *and* a discussion.
/// </para>
/// <para>
/// <see cref="ImageUrl"/> is best-effort preview enrichment for client UI and is
/// frequently null; it is never required for a well-formed item.
/// <see cref="CommentNote"/> is populated only for stories whose discussion the
/// model actually read, and when present it is a detailed multi-sentence
/// paragraph rather than a one-liner — clients should lay it out accordingly.
/// </para>
/// </summary>
public record DigestItemDto(
    int StoryId,
    string Title,
    string? Url,
    string HackerNewsUrl,
    string Summary,
    string Author,
    string? CommentNote = null,
    string? ImageUrl = null
);

/// <summary>
/// One entry in the digest taxonomy, as served by GET /digest/categories.
/// <para>
/// An object rather than a bare string so that adding a field later (a slug, a
/// description) is an additive, non-breaking change; turning a JSON string into
/// an object would not be. The <b>array order is the contract</b> — the server's
/// sort order is deliberately kept off the wire so no client can re-derive, or
/// disagree about, the ordering.
/// </para>
/// <para>
/// <see cref="IsEnabled"/> is only meaningful on the admin listing; the public
/// endpoint returns enabled categories only, so it is always true there.
/// </para>
/// </summary>
public record DigestTaxonomyCategoryDto(string Name, bool IsEnabled = true);

/// <summary>
/// Result of asking the server to make sure today's digest exists.
/// <para>
/// <see cref="Status"/> is one of "AlreadyCurrent", "Started" or "InProgress".
/// A string rather than a client-side enum so adding a state later doesn't break
/// an older client's deserialization.
/// </para>
/// <para>
/// <see cref="GeneratedAtUtc"/> is the timestamp of the digest already on the
/// server, when there is one — null before the very first run.
/// </para>
/// </summary>
public record DigestGenerationStatusDto(string Status, DateTime? GeneratedAtUtc);

/// <summary>Request body for adding a category to the taxonomy.</summary>
public record AddDigestCategoryRequest(string Name);

/// <summary>Request body for enabling or disabling an existing category.</summary>
public record SetDigestCategoryEnabledRequest(bool IsEnabled);

/// <summary>
/// Request body for reordering the taxonomy. The full ordered list of names is
/// supplied rather than a single index move: that makes the operation idempotent
/// and removes all index arithmetic, and it lets the server reject anything that
/// isn't a permutation of what it already has instead of silently half-applying.
/// </summary>
public record ReorderDigestCategoriesRequest(IReadOnlyList<string> Names);
