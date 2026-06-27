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

    [JsonIgnore]
    public string? RootDomain
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Url))
                return null;

            try
            {
                var host = new Uri(Url).Host;
                var parts = host.Split('.');

                if (parts.Length >= 2) return string.Join('.', parts[^2..]);

                return host;
            }
            catch
            {
                return null;
            }
        }
    }

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
