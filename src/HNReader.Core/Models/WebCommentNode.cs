using HNReader.Core.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HNReader.Core.Models;

/// <summary>
/// A CommentNode specifically for comments parsed from the web.
/// The depth is already known from the HTML parsing, making tree construction trivial.
/// </summary>
public class WebCommentNode : INotifyPropertyChanged
{
    public WebCommentNode(WebComment comment)
    {
        Children = [];
        _depth = comment.Depth;
        CommentId = comment.Id;

        // Defer HTML→Markdown conversion until the comment is actually rendered.
        // For long threads this avoids running the regex on hundreds of comments
        // that are collapsed or off-screen — the Lazy<string> only fires on first
        // access of MdText (typically when the MarkdownTextBlock is realized).
        _mdTextLazy = new Lazy<string?>(
            () => HtmlContentHelper.ToMarkdown(comment?.Text),
            LazyThreadSafetyMode.PublicationOnly);

        By = comment?.By ?? string.Empty;
        TimeAgo = comment?.TimeAgo;
        _time = comment?.Time;
    }

    /// <summary>
    /// The HN comment ID.
    /// </summary>
    public int CommentId { get; }

    public ObservableCollection<WebCommentNode> Children { get; }

    private readonly Lazy<string?> _mdTextLazy;

    private int _depth;
    public int Depth
    {
        get => _depth;
        set
        {
            if (_depth == value) return;
            _depth = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Indent));
        }
    }

    public string Indent => $"{Depth * 16},0,0,0";

    private bool _isCollapsed;
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed == value) return;
            _isCollapsed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CollapseIcon));
        }
    }

    private bool _isHighlighted;
    /// <summary>
    /// Temporarily set to true when a search or reference scrolls to this comment.
    /// The UI binds to this to show a highlight border/background for a few seconds.
    /// </summary>
    public bool IsHighlighted
    {
        get => _isHighlighted;
        set
        {
            if (_isHighlighted == value) return;
            _isHighlighted = value;
            OnPropertyChanged();
        }
    }

    public string By { get; set; }
    public string? TimeAgo { get; set; }

    private readonly long? _time;
    /// <summary>
    /// Unix timestamp of when the comment was posted.
    /// </summary>
    public long? Time => _time;

    /// <summary>
    /// The comment body, converted from HN HTML to Markdown on first access.
    /// Subsequent reads return the cached value. Conversion is skipped for any
    /// comment whose MarkdownTextBlock is never realized (collapsed parents,
    /// off-screen items in a virtualized list, etc.).
    /// </summary>
    public string? MdText => _mdTextLazy.Value;

    public string CollapseIcon => IsCollapsed ? "\uE76C" : "\uE76B";

    public void ToggleCollapsed()
    {
        IsCollapsed = !IsCollapsed;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
