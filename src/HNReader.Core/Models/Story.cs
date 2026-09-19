using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HNReader.Core.Helpers;

namespace HNReader.Core.Models;

public class Story : BaseHNItem, INotifyPropertyChanged
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonIgnore]
    public string? MarkdownText => HtmlContentHelper.ToMarkdown(Text);

    [JsonPropertyName("score")]
    public int? Score { get; set; }

    [JsonPropertyName("kids")]
    public List<int>? Kids { get; set; }

    private int? _descendants;

    /// <summary>
    /// The total comment count as reported by the HN API. May be 0 for some stories
    /// (especially Ask HN) due to known inconsistencies in the Firebase API;
    /// callers should refresh this from the actual HTML page if an accurate count
    /// is critical.
    /// </summary>
    [JsonPropertyName("descendants")]
    public int? Descendants
    {
        get => _descendants;
        set
        {
            if (_descendants == value) return;
            _descendants = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CommentCount));
        }
    }

    /// <summary>
    /// Display-friendly comment count (returns 0 if null).
    /// </summary>
    [JsonIgnore]
    public int CommentCount => Descendants ?? 0;

    /// <summary>
    /// Display-friendly score (returns 0 if null).
    /// </summary>
    [JsonIgnore]
    public int DisplayScore => Score ?? 0;

    /// <summary>
    /// Registrable domain of <see cref="Url"/> - <c>bbc.co.uk</c>, not <c>co.uk</c>.
    /// Kept for the search filter and the WinUI binding that already consume it.
    /// For fetching an icon use <see cref="FaviconHost"/>; for captions use
    /// <see cref="DisplayDomain"/>.
    /// </summary>
    [JsonIgnore]

    public string? RootDomain =>
        DomainHelper.GetRegistrableDomain(Url) ?? DomainHelper.GetFaviconHost(Url);

    /// <summary>
    /// Full host minus <c>www.</c> - the key a favicon is actually fetched with.
    /// Truncating this to the apex is what made icons for sub-domain hosted sites
    /// (<c>simonw.github.io</c>, <c>x.substack.com</c>) resolve to the wrong site or
    /// to nothing at all.
    /// </summary>
    [JsonIgnore]

    public string? FaviconHost => DomainHelper.GetFaviconHost(Url);

    /// <summary>The domain shown under the title.</summary>
    [JsonIgnore]

    public string? DisplayDomain => DomainHelper.GetDisplayDomain(Url);

    private bool _isFavorite;

    /// <summary>
    /// Indicates whether this story is in the user's favorites.
    /// This is not persisted in JSON - it's set at runtime.
    /// </summary>
    [JsonIgnore]
    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite != value)
            {
                _isFavorite = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
