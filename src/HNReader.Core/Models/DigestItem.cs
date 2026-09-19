using CommunityToolkit.Mvvm.ComponentModel;
using HNReader.Core.Helpers;
using HNReader.Shared.Models;

namespace HNReader.Core.Models;

/// <summary>
/// One digest story, prepared for binding.
/// <para>
/// The observable counterpart of <see cref="DigestItemDto"/>, mirroring the
/// existing <see cref="WebComment"/> → <see cref="WebCommentNode"/> split: the
/// DTO is the wire shape and lives in HNReader.Shared, which is deliberately
/// free of MVVM dependencies, so anything with view state belongs here.
/// </para>
/// </summary>
public partial class DigestItem : ObservableObject
{
    public int StoryId { get; init; }
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The story's own article link, or null for an HN self-post (Ask HN, a text
    /// post, a Show HN without a link). Null here is normal, not missing data —
    /// see <see cref="PrimaryUrl"/>.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>The HN discussion page. Always present.</summary>
    public string HackerNewsUrl { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;

    /// <summary>
    /// The model's account of the story's discussion, when it read it. A
    /// detailed multi-sentence paragraph rather than a one-liner, which is why
    /// it is collapsed by default in the UI.
    /// </summary>
    public string? CommentNote { get; init; }

    /// <summary>Best-effort preview image. Frequently null; not an error.</summary>
    public string? ImageUrl { get; init; }

    /// <summary>
    /// Whether the discussion note is expanded. The only mutable state on this
    /// type — and the reason the digest list uses a non-virtualized ItemsControl,
    /// so expansion can't be recycled onto a different row.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoteToggleText))]
    private bool isNoteExpanded;

    public bool HasNote => !string.IsNullOrWhiteSpace(CommentNote);

    public bool HasImage => !string.IsNullOrWhiteSpace(ImageUrl);

    /// <summary>Whether this story links out to an article at all.</summary>
    public bool HasArticle => !string.IsNullOrWhiteSpace(Url);

    /// <summary>Where "open this story" should go: the article if there is one, else the thread.</summary>
    public string PrimaryUrl => HasArticle ? Url! : HackerNewsUrl;

    public string NoteToggleText => IsNoteExpanded ? "Hide discussion notes" : "What commenters said";

    /// <summary>
    /// Registrable domain of <see cref="Url"/> - <c>bbc.co.uk</c>, not <c>co.uk</c>.
    /// Kept for the search filter and the WinUI binding that already consume it.
    /// For fetching an icon use <see cref="FaviconHost"/>; for captions use
    /// <see cref="DisplayDomain"/>.
    /// </summary>
    public string? RootDomain =>
        DomainHelper.GetRegistrableDomain(Url) ?? DomainHelper.GetFaviconHost(Url);

    /// <summary>
    /// Full host minus <c>www.</c> - the key a favicon is actually fetched with.
    /// Truncating this to the apex is what made icons for sub-domain hosted sites
    /// (<c>simonw.github.io</c>, <c>x.substack.com</c>) resolve to the wrong site or
    /// to nothing at all.
    /// </summary>
    public string? FaviconHost => DomainHelper.GetFaviconHost(Url);

    /// <summary>The domain shown under the title.</summary>
    public string? DisplayDomain => DomainHelper.GetDisplayDomain(Url);

    public static DigestItem From(DigestItemDto dto) => new()
    {
        StoryId = dto.StoryId,
        Title = dto.Title,
        Url = dto.Url,
        HackerNewsUrl = dto.HackerNewsUrl,
        Summary = dto.Summary,
        Author = dto.Author,
        CommentNote = dto.CommentNote,
        ImageUrl = dto.ImageUrl
    };
}
