using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Net;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using HNReader.Avalonia.Services;
using HNReader.Core.Helpers;
using HNReader.Core.Models;
using HNReader.Core.Services.Logging;
using HNReader.Core.Viewmodels;
using Microsoft.Extensions.DependencyInjection;

namespace HNReader.Avalonia.Controls;

public partial class StoriesPageControl : UserControl
{
    private PageViewModel? _currentViewModel;
    private ScrollViewer? _storiesScrollViewer;

    /// <summary>
    /// Every currently visible comment, in display order, parents before children.
    /// </summary>
    /// <remarks>
    /// The view model exposes the comment tree as roots with nested Children, which the
    /// UI used to mirror with a template that recursed into an ItemsControl per level.
    /// Flattening it costs the same to render but avoids nesting the visual tree eleven
    /// deep on a long thread, and lets depth be drawn in one pass instead of built from
    /// a Panel and Border per level (see CommentRails).
    ///
    /// The whole list is rendered - there is no windowing or virtualization here, and
    /// that is deliberate. Anything that adds comments while the reader scrolls changes
    /// the scroll extent underneath them, which moves the scrollbar thumb; anything that
    /// virtualizes replaces the true extent with an estimate that is revised on every
    /// realization. Both show up as a scrollbar that twitches. Rendering everything up
    /// front costs one expensive layout when the thread loads and then leaves the extent
    /// fixed for the life of the view, which is what makes scrolling steady.
    ///
    /// Built here rather than in HNReader.Core because Core is shared with the WinUI
    /// shell, which renders the same tree with its own nested control and would gain
    /// nothing from a second collection to keep in step.
    /// </remarks>
    ///
    /// An AvaloniaList rather than an ObservableCollection because collapsing or expanding
    /// one comment inserts or removes a whole run of rows, and AvaloniaList raises that as a
    /// single range notification instead of one per row.
    private readonly AvaloniaList<WebCommentNode> _flatComments = [];

    /// <summary>
    /// The root collection currently being watched, so it can be unsubscribed.
    /// </summary>
    private ObservableCollection<WebCommentNode>? _observedRoots;

    // How close to the bottom (in pixels) triggers an auto load-more, so it
    // fires a little before the user hits the literal end of the list.
    private const double InfiniteScrollThreshold = 200;

    public StoriesPageControl()
    {
        InitializeComponent();
        CommentsItems.ItemsSource = _flatComments;
        DataContextChanged += OnDataContextChanged;
        StoriesList.TemplateApplied += OnStoriesListTemplateApplied;
    }

    private void OnStoriesListTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        if (_storiesScrollViewer != null)
        {
            _storiesScrollViewer.ScrollChanged -= OnStoriesListScrollChanged;
        }

        _storiesScrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        if (_storiesScrollViewer != null)
        {
            _storiesScrollViewer.ScrollChanged += OnStoriesListScrollChanged;
        }
    }

    private async void OnStoriesListScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_storiesScrollViewer == null || DataContext is not PageViewModel vm) return;

        if (!vm.LoadMoreStoriesCommand.CanExecute(null)) return;

        // After a failure, wait for the user's explicit "Try again" rather than
        // re-firing the same failing request on every scroll tick.
        if (vm.HasLoadMoreError) return;

        var distanceFromBottom = _storiesScrollViewer.Extent.Height
            - _storiesScrollViewer.Viewport.Height
            - _storiesScrollViewer.Offset.Y;

        if (distanceFromBottom > InfiniteScrollThreshold) return;

        await ExecuteUiActionSafelyAsync(
            () => vm.LoadMoreStoriesCommand.ExecuteAsync(null),
            "auto-loading more stories on scroll");
    }

    private void OnDataContextChanged(object? sender, EventArgs args)
    {
        if (_currentViewModel != null)
        {
            _currentViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (DataContext is PageViewModel vm)
        {
            _currentViewModel = vm;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
        else
        {
            _currentViewModel = null;
        }

        // Re-point at the new view model's collection, not just rebuild from it: without
        // this the CollectionChanged subscription stays on the previous page's collection
        // until that page happens to reassign WebCommentNodes.
        WatchCommentRoots();
        RebuildFlatComments();
        ClearDetailFavicon();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The whole collection is swapped on load, clear and retry.
        if (e.PropertyName == nameof(PageViewModel.WebCommentNodes))
        {
            WatchCommentRoots();
            RebuildFlatComments();
        }
        else if (e.PropertyName == nameof(PageViewModel.SelectedStory))
        {
            ClearDetailFavicon();
        }
    }

    /// <summary>
    /// Drops the favicon so the next story cannot inherit the previous one.
    /// </summary>
    /// <remarks>
    /// AsyncImageLoader 3.7.0 assigns Image.Source only when a bitmap comes back
    /// non-null - its handler ends with "if (bitmap != null ...) sender.Source = bitmap".
    /// FaviconImageLoader correctly returns null for a host with no favicon, and the
    /// library then leaves whatever was there before, so the previous story's icon stayed
    /// on screen. (It does clear when the bound URL itself is empty, which is why a story
    /// with no URL never showed this.) Fixed upstream after 3.7.0, but that fix arrived
    /// with a restructure of the loader interface which FaviconImageLoader sits on,
    /// so the upgrade wants to be its own change.
    ///
    /// Only the detail pane needs this: every other ImageLoader.Source call site is inside
    /// an item template and gets a fresh Image per item, with no previous bitmap to go
    /// stale.
    ///
    /// Ordering does not matter. This and the XAML binding both run off the same
    /// PropertyChanged, but the library yields at an await - behind a 10ms delay - before
    /// it ever assigns, so its assignment lands after this synchronous clear whichever of
    /// the two runs first.
    /// </remarks>
    private void ClearDetailFavicon() => DetailFavicon.Source = null;

    /// <summary>
    /// Follows the view model's current root collection.
    /// </summary>
    /// <remarks>
    /// Watching the collection matters as much as watching the property: the view
    /// model streams roots in batches into the collection it already published
    /// (PageViewModel.LoadWebComments), so after the first assignment every
    /// subsequent comment arrives as a CollectionChanged notification and no
    /// property change is raised at all.
    /// </remarks>
    private void WatchCommentRoots()
    {
        if (_observedRoots is not null)
        {
            _observedRoots.CollectionChanged -= OnCommentRootsChanged;
        }

        _observedRoots = _currentViewModel?.WebCommentNodes;

        if (_observedRoots is not null)
        {
            _observedRoots.CollectionChanged += OnCommentRootsChanged;
        }
    }

    private void OnCommentRootsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Appending the new roots keeps the batched load from resetting the list
        // - and the user's scroll position with it - once per batch.
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is WebCommentNode node) CommentFlattener.AppendVisible(node, _flatComments);
            }

            return;
        }

        RebuildFlatComments();
    }

    private void RebuildFlatComments()
    {
        var visible = _currentViewModel?.WebCommentNodes is { } roots
            ? CommentFlattener.Flatten(roots)
            : [];

        _flatComments.Clear();
        _flatComments.AddRange(visible);
    }

    /// <summary>
    /// Patches the flat list for one node whose collapsed state has just changed, touching only
    /// that node's replies. Every other row keeps its realized controls, so nothing outside the
    /// subtree is measured again or has its markdown re-rendered.
    /// </summary>
    private void ApplyCollapseChange(WebCommentNode node)
    {
        var index = _flatComments.IndexOf(node);
        if (index < 0)
        {
            // Not in the list (e.g. the roots were swapped under us): fall back to a rebuild.
            RebuildFlatComments();
            return;
        }

        if (node.IsCollapsed)
        {
            var count = CommentFlattener.CountSubtreeItemsAfter(_flatComments, index);
            if (count > 0) _flatComments.RemoveRange(index + 1, count);
        }
        else
        {
            var revealed = new List<WebCommentNode>();
            CommentFlattener.AppendVisibleDescendants(node, revealed);
            if (revealed.Count > 0) _flatComments.InsertRange(index + 1, revealed);
        }
    }

    /// <summary>
    /// Called by MainWindow in response to the Ctrl+F shortcut.
    /// </summary>
    /// <remarks>
    /// A no-op on pages without a search box - only Favourites opts in - so the shortcut
    /// does not move focus somewhere invisible.
    /// </remarks>
    public void FocusSearchBox()
    {
        if (DataContext is PageViewModel { IsSearchEnabled: true })
        {
            SearchBox.Focus();
        }
    }

    private ILogger? ResolveLogger() => (global::Avalonia.Application.Current as App)?.Services.GetService<ILogger>();

    private NotificationService? ResolveNotifications() => (global::Avalonia.Application.Current as App)?.Services.GetService<NotificationService>();

    private async Task ExecuteUiActionSafelyAsync(Func<Task> action, string operationName, string? userMessageOnFailure = null)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ResolveLogger()?.LogError("StoriesPageControl", $"Error during {operationName}", ex);

            // The log has the detail; the user gets one plain sentence, not silence.
            if (userMessageOnFailure is not null)
            {
                ResolveNotifications()?.ShowError(userMessageOnFailure);
            }
        }
    }

    private void OnToggleWebCommentCollapseClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Control fe && fe.Tag is WebCommentNode node)
            {
                PageViewModel.ToggleWebCommentCollapse(node);

                // The tree carries the collapsed flag, but the flat list is what the
                // list reads, so just this node's replies are removed or inserted.
                ApplyCollapseChange(node);
            }
        }
        catch (Exception ex)
        {
            ResolveLogger()?.LogError("StoriesPageControl", "Exception in OnToggleWebCommentCollapseClicked", ex);
        }
    }

    private async void OnToggleFavoriteClicked(object? sender, RoutedEventArgs e)
    {
        await ExecuteUiActionSafelyAsync(async () =>
        {
            if (DataContext is not PageViewModel vm) return;
            if (!vm.ToggleFavoriteCommand.CanExecute(null)) return;

            await vm.ToggleFavoriteCommand.ExecuteAsync(null);

            ResolveNotifications()?.ShowSuccess(
                vm.IsSelectedStoryFavorite ? "Added to favourites" : "Removed from favourites");
        }, "toggling favorite", "Couldn't update your favourites. Please try again.");
    }

    private async void OnViewOnHnClicked(object? sender, RoutedEventArgs e)
    {
        await ExecuteUiActionSafelyAsync(() =>
        {
            if (DataContext is not PageViewModel vm || vm.SelectedStory == null) return Task.CompletedTask;

            var hnUrl = $"https://news.ycombinator.com/item?id={vm.SelectedStory.Id}";
            if (!UrlOpener.TryOpen(hnUrl))
            {
                ResolveNotifications()?.ShowError("Couldn't open the link in your browser.");
            }
            return Task.CompletedTask;
        }, "opening story on Hacker News");
    }

    private void OnOpenStoryUrlClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not PageViewModel vm || vm.SelectedStory?.Url == null) return;
            if (!TryLaunch(vm.SelectedStory.Url))
            {
                ResolveNotifications()?.ShowError("Couldn't open the link in your browser.");
            }
        }
        catch (Exception ex)
        {
            ResolveLogger()?.LogError("StoriesPageControl", "Error opening story link", ex);
        }
    }

    private static bool TryLaunch(string? link)
    {
        if (string.IsNullOrWhiteSpace(link)) return false;

        link = WebUtility.HtmlDecode(link);

        if (!Uri.TryCreate(link, UriKind.Absolute, out _))
        {
            if (!link.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                link = $"https://{link}";
            }
            else
            {
                return false;
            }
        }

        return UrlOpener.TryOpen(link);
    }
}
