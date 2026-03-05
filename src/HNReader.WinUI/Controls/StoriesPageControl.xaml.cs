using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using HNReader.Core.Models;
using HNReader.Core.Viewmodels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.ApplicationModel.DataTransfer;
using CommunityToolkit.WinUI.UI.Controls;
using Windows.System;

namespace HNReader.WinUI.Controls;

public sealed partial class StoriesPageControl : UserControl
{
    private int _previousStoryCount = 0;
    private bool _isInsightsPanelOpen = false;
    private const double InsightsPanelWidth = 380;
    private PageViewModel? _currentViewModel;

    public StoriesPageControl()
    {
        InitializeComponent();

        StoriesList.SelectionChanged += StoriesList_SelectionChanged;
        StoriesList.Loaded += StoriesList_Loaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        // Unsubscribe from previous ViewModel
        if (_currentViewModel != null)
        {
            _currentViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        // Subscribe to new ViewModel
        if (args.NewValue is PageViewModel vm)
        {
            _currentViewModel = vm;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            // Sync initial panel state
            SyncPanelStateFromViewModel(vm);
        }
        else
        {
            _currentViewModel = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not PageViewModel vm) return;

        switch (e.PropertyName)
        {
            case nameof(PageViewModel.IsInsightsPanelOpen):
                SyncPanelStateFromViewModel(vm);
                break;
            case nameof(PageViewModel.SelectedStory):
                // When story becomes null (e.g., on refresh), ensure panel is closed
                if (vm.SelectedStory == null && _isInsightsPanelOpen)
                {
                    CloseInsightsPanel();
                }
                break;
        }
    }

    /// <summary>
    /// Syncs the visual panel state from the ViewModel's IsInsightsPanelOpen property.
    /// </summary>
    private void SyncPanelStateFromViewModel(PageViewModel vm)
    {
        if (vm.IsInsightsPanelOpen && !_isInsightsPanelOpen)
        {
            OpenInsightsPanel();
        }
        else if (!vm.IsInsightsPanelOpen && _isInsightsPanelOpen)
        {
            CloseInsightsPanel();
        }
    }

    private void OpenInsightsPanel()
    {
        _isInsightsPanelOpen = true;

        // Fade in: start transparent, expand column, then animate opacity
        AiInsightsPanel.Opacity = 0;
        AssistantColumn.Width = new GridLength(InsightsPanelWidth);
        AssistantColumn.MinWidth = 280;

        // Show the splitter between detail and insights
        SplitterColumn.Width = GridLength.Auto;
        InsightsSplitter.Visibility = Visibility.Visible;

        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(250)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(fadeIn, AiInsightsPanel);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");

        var sb = new Storyboard();
        sb.Children.Add(fadeIn);
        sb.Begin();

        AiInsightsButtonText.Text = "Close Insights";
    }

    private void CloseInsightsPanel()
    {
        _isInsightsPanelOpen = false;

        // Fade out: animate opacity to 0, then collapse column
        var fadeOut = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(fadeOut, AiInsightsPanel);
        Storyboard.SetTargetProperty(fadeOut, "Opacity");

        var sb = new Storyboard();
        sb.Children.Add(fadeOut);
        sb.Completed += (s, e) =>
        {
            AssistantColumn.Width = new GridLength(0);
            AssistantColumn.MinWidth = 0;

            // Hide the splitter when insights panel is collapsed
            SplitterColumn.Width = new GridLength(0);
            InsightsSplitter.Visibility = Visibility.Collapsed;
        };
        sb.Begin();

        AiInsightsButtonText.Text = "AI Insights";
    }

    private void StoriesList_Loaded(object sender, RoutedEventArgs e)
    {
        // Find the ScrollViewer inside the ListView
        if (GetScrollViewer(StoriesList) is ScrollViewer scrollViewer)
        {
            scrollViewer.ViewChanged += ScrollViewer_ViewChanged;
        }
    }

    private ScrollViewer? GetScrollViewer(DependencyObject element)
    {
        if (element is ScrollViewer scrollViewer)
            return scrollViewer;

        for (int i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(element); i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(element, i);
            var result = GetScrollViewer(child);
            if (result != null)
                return result;
        }

        return null;
    }

    private void ScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        // Scroll detection can be used for future infinite scroll if needed
        // Currently the Load More button is always visible when there are more items
    }

    private async void OnLoadMoreClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PageViewModel vm) return;
        if (!vm.LoadMoreStoriesCommand.CanExecute(null)) return;

        // Store current count before loading
        _previousStoryCount = vm.Stories.Count;

        await vm.LoadMoreStoriesCommand.ExecuteAsync(null);

        // After loading, scroll to the first new item
        await ScrollToFirstNewItem();
    }

    private async Task ScrollToFirstNewItem()
    {
        if (DataContext is not PageViewModel vm) return;
        if (vm.Stories.Count <= _previousStoryCount) return;

        // Small delay to let the UI update
        await Task.Delay(100);

        // Scroll to the first newly loaded item
        if (_previousStoryCount < vm.Stories.Count)
        {
            var firstNewItem = vm.Stories[_previousStoryCount];
            StoriesList.ScrollIntoView(firstNewItem, ScrollIntoViewAlignment.Leading);
        }
    }

    private void StoriesList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is PageViewModel vm)
        {
            if (StoriesList.SelectedItem is Story selected)
            {
                vm.SelectedStory = selected;
            }
            else
            {
                vm.SelectedStory = null;
            }
        }
    }

    // Click handler for web-parsed comment collapse buttons
    private void OnToggleWebCommentCollapseClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement fe && fe.DataContext is WebCommentNode node && DataContext is PageViewModel vm)
            {
                var dq = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                if (dq != null)
                {
                    dq.TryEnqueue(() => PageViewModel.ToggleWebCommentCollapse(node));
                }
                else
                {
                    node.ToggleCollapsed();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Exception in OnToggleWebCommentCollapseClicked: {ex}");
            throw;
        }
    }

    private async void OnCopyLinkClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not PageViewModel vm || vm.SelectedStory?.Url == null) return;

            var dataPackage = new DataPackage();
            dataPackage.SetText(vm.SelectedStory.Url);
            Clipboard.SetContent(dataPackage);

            await vm.ShowCopyFeedbackAsync("Link copied!");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error copying link: {ex}");
        }
    }

    private async void OnCopyTitleClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not PageViewModel vm || vm.SelectedStory?.Title == null) return;

            var dataPackage = new DataPackage();
            dataPackage.SetText(vm.SelectedStory.Title);
            Clipboard.SetContent(dataPackage);

            await vm.ShowCopyFeedbackAsync("Title copied!");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error copying title: {ex}");
        }
    }

    private void OnShareClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not PageViewModel vm || vm.SelectedStory == null) return;

            var dataTransferManager = DataTransferManagerInterop.GetForWindow(GetWindowHandle());
            dataTransferManager.DataRequested += (s, args) =>
            {
                var request = args.Request;
                request.Data.Properties.Title = vm.SelectedStory.Title ?? "Hacker News Story";
                request.Data.Properties.Description = "Shared from HN Reader";
                
                if (!string.IsNullOrEmpty(vm.SelectedStory.Url))
                {
                    request.Data.SetWebLink(new Uri(vm.SelectedStory.Url));
                }
                
                var shareText = $"{vm.SelectedStory.Title}\n\n{vm.SelectedStory.Url ?? ""}\n\nShared from HN Reader";
                request.Data.SetText(shareText);
            };

            DataTransferManagerInterop.ShowShareUIForWindow(GetWindowHandle());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error sharing: {ex}");
        }
    }

    private async void OnViewOnHnClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not PageViewModel vm || vm.SelectedStory == null) return;

            var hnUrl = $"https://news.ycombinator.com/item?id={vm.SelectedStory.Id}";
            await Launcher.LaunchUriAsync(new Uri(hnUrl));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error opening HN: {ex}");
        }
    }

    private static IntPtr GetWindowHandle()
    {
        var window = App.CurrentWindow;
        if (window != null)
        {
            return WinRT.Interop.WindowNative.GetWindowHandle(window);
        }
        return IntPtr.Zero;
    }

    private async void OnCommentMarkdownLinkClicked(object sender, LinkClickedEventArgs e)
    {
        await TryLaunchAsync(e.Link);
    }

    private async void OnStoryMarkdownLinkClicked(object sender, LinkClickedEventArgs e)
    {
        await TryLaunchAsync(e.Link);
    }

    private static async Task<bool> TryLaunchAsync(string? link)
    {
        if (string.IsNullOrWhiteSpace(link)) return false;

        link = WebUtility.HtmlDecode(link);

        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
            // Attempt to prepend https scheme if missing
            if (!link.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                link = $"https://{link}";
                link = WebUtility.HtmlDecode(link);
                if (!Uri.TryCreate(link, UriKind.Absolute, out uri))
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        return await Launcher.LaunchUriAsync(uri);
    }

    private void OnToggleInsightsPanelClicked(object sender, RoutedEventArgs e)
    {
        var newState = !_isInsightsPanelOpen;
        
        if (newState)
        {
            OpenInsightsPanel();
        }
        else
        {
            CloseInsightsPanel();
        }

        // Sync state back to ViewModel (for caching purposes)
        if (DataContext is PageViewModel vm)
        {
            vm.IsInsightsPanelOpen = newState;
        }
    }

    /// <summary>
    /// Handles link clicks in the AI insight MarkdownTextBlock.
    /// Links using "https://hn-comment/{commentId}" trigger scroll-to-comment.
    /// All other links open in the default browser.
    /// </summary>
    private async void OnInsightMarkdownLinkClicked(object sender, LinkClickedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Link)) return;

        var link = e.Link.Trim();

        // Check for comment reference links (e.g., "https://hn-comment/12345")
        const string commentProtocol = "https://hn-comment/";
        if (link.StartsWith(commentProtocol, StringComparison.OrdinalIgnoreCase))
        {
            var idString = link[commentProtocol.Length..].TrimEnd('/');
            if (int.TryParse(idString, out var commentId) && commentId > 0)
            {
                await ScrollToCommentByIdAsync(commentId);
                return;
            }
            Debug.WriteLine($"Invalid comment ID in link: {link}");
            return;
        }

        // Fallback: check for legacy @author format (e.g., "@username")
        if (link.StartsWith("@") || link.StartsWith("%40"))
        {
            var author = link.TrimStart('@').Trim();
            author = WebUtility.UrlDecode(author);
            if (author.StartsWith("@")) author = author[1..];
            await ScrollToCommentByAuthorAsync(author);
            return;
        }

        // Regular URL — open in browser
        await TryLaunchAsync(link);
    }

    /// <summary>
    /// Scrolls the comments section to a comment identified by its unique numeric ID
    /// and highlights it temporarily with a visible border.
    /// Called from AI insight hn-comment:// reference links.
    /// </summary>
    private async Task ScrollToCommentByIdAsync(int commentId)
    {
        if (DataContext is not PageViewModel vm) return;

        // Find the comment by ID in the comment tree
        var targetNode = vm.FindCommentById(commentId);
        if (targetNode == null)
        {
            Debug.WriteLine($"Could not find comment with ID: {commentId}");
            return;
        }

        // Ensure comments section is visible and loaded
        if (!vm.AreCommentsVisible)
        {
            if (vm.ToggleCommentsCommand.CanExecute(null))
            {
                await vm.ToggleCommentsCommand.ExecuteAsync(null);
            }
            // Wait for comments to render
            await Task.Delay(300);
        }

        // Highlight the comment (ViewModel handles expanding collapsed parents)
        _ = vm.HighlightCommentAsync(targetNode, 3000);

        // Allow UI to update after expanding collapsed parents
        await Task.Delay(100);

        // Scroll to the comment within the ScrollViewer
        ScrollToCommentNode(targetNode);
    }

    /// <summary>
    /// Legacy fallback: scrolls to a comment by author name.
    /// Used when the AI output uses the older @author format without a comment ID.
    /// </summary>
    private async Task ScrollToCommentByAuthorAsync(string author)
    {
        if (DataContext is not PageViewModel vm) return;

        var targetNode = vm.FindCommentByAuthor(author);
        if (targetNode == null)
        {
            Debug.WriteLine($"Could not find comment by author: {author}");
            return;
        }

        if (!vm.AreCommentsVisible)
        {
            if (vm.ToggleCommentsCommand.CanExecute(null))
            {
                await vm.ToggleCommentsCommand.ExecuteAsync(null);
            }
            await Task.Delay(300);
        }

        _ = vm.HighlightCommentAsync(targetNode, 3000);
        await Task.Delay(100);
        ScrollToCommentNode(targetNode);
    }

    /// <summary>
    /// Walks the visual tree inside the comments ScrollViewer to find the Border
    /// with a Tag matching the target comment's ID, then scrolls it into view.
    /// </summary>
    private void ScrollToCommentNode(WebCommentNode targetNode)
    {
        try
        {
            // Find the Border element with the matching CommentId Tag
            var targetElement = FindCommentBorderInVisualTree(WebCommentsItemsControl, targetNode.CommentId);
            if (targetElement == null)
            {
                Debug.WriteLine($"Could not find visual element for comment ID: {targetNode.CommentId}");
                return;
            }

            // Calculate position relative to the ScrollViewer and scroll to it
            var transform = targetElement.TransformToVisual(CommentsScrollViewer);
            var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));

            // Scroll so the comment is near the top of the viewport with some padding
            var targetOffset = CommentsScrollViewer.VerticalOffset + position.Y - 80;
            CommentsScrollViewer.ChangeView(null, Math.Max(0, targetOffset), null, false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error scrolling to comment: {ex}");
        }
    }

    /// <summary>
    /// Recursively searches the visual tree for a Border element whose Tag property
    /// matches the given comment ID. Used for scroll-to-comment from AI insights.
    /// </summary>
    private static FrameworkElement? FindCommentBorderInVisualTree(DependencyObject? parent, int commentId)
    {
        if (parent == null) return null;

        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            
            // Check if this is the target Border with matching CommentId Tag
            if (child is Border border && border.Tag is int tagId && tagId == commentId)
            {
                return border;
            }

            // Recurse into children
            var result = FindCommentBorderInVisualTree(child, commentId);
            if (result != null) return result;
        }

        return null;
    }
}
