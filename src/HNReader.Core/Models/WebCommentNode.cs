using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HNReader.Core.Helpers;

namespace HNReader.Core.Models;

/// <summary>
/// A CommentNode specifically for comments parsed from the web.
/// The depth is already known from the HTML parsing, making tree construction trivial.
/// </summary>
public class WebCommentNode : INotifyPropertyChanged
{
    public WebCommentNode(WebComment comment)
    {
        Comment = comment;
        Children = [];
        _depth = comment.Depth;
        
        // Pre-compute and cache the parsed text to avoid re-parsing on every UI access
        _markdownText = HtmlContentHelper.ToMarkdown(comment?.Text);
    }

    public WebComment Comment { get; }
    public ObservableCollection<WebCommentNode> Children { get; }

    private readonly string? _markdownText;

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

    public string By => Comment?.By ?? string.Empty;
    public string? TimeAgo => Comment?.TimeAgo;

    // Cached text to avoid re-parsing HTML on every UI access
    public string? MarkdownText => _markdownText;

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
