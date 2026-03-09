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

        // Use the fast string-replacement HTML→Markdown path.
        _mdText = HtmlContentHelper.ToMarkdown(comment?.Text);

        By = comment?.By ?? string.Empty;
        TimeAgo = comment?.TimeAgo;
    }

    /// <summary>
    /// The HN comment ID, used for scroll-to-comment from AI insight references.
    /// </summary>
    public int CommentId { get; }

    public ObservableCollection<WebCommentNode> Children { get; }

    private readonly string? _mdText;

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
    /// Temporarily set to true when an AI insight reference scrolls to this comment.
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

    public string By { get; set;  }
    public string? TimeAgo { get; set; }

    // Cached text to avoid re-parsing HTML on every UI access
    public string? MdText => _mdText;

    // Icon changes based on collapsed state
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
