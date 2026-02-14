using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using HNReader.Core.Models;
using HNReader.Core.Viewmodels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
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
        AssistantColumn.Width = new GridLength(InsightsPanelWidth);
        AiInsightsButtonText.Text = "Close Insights";
    }

    private void CloseInsightsPanel()
    {
        _isInsightsPanelOpen = false;
        AssistantColumn.Width = new GridLength(0);
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

    private async System.Threading.Tasks.Task ScrollToFirstNewItem()
    {
        if (DataContext is not PageViewModel vm) return;
        if (vm.Stories.Count <= _previousStoryCount) return;

        // Small delay to let the UI update
        await System.Threading.Tasks.Task.Delay(100);

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
            if (sender is FrameworkElement fe && fe.DataContext is WebCommentNode node)
            {
                // Simply toggle the collapsed state - the recursive template handles visibility via binding
                var dq = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                if (dq != null)
                {
                    dq.TryEnqueue(() => node.ToggleCollapsed());
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
            await Windows.System.Launcher.LaunchUriAsync(new Uri(hnUrl));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error opening HN: {ex}");
        }
    }

    private IntPtr GetWindowHandle()
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
}
