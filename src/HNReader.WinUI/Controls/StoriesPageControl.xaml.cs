using System;
using System.ComponentModel;
using System.Net;
using System.Threading.Tasks;
using HNReader.Core.Models;
using HNReader.Core.Services.Logging;
using HNReader.Core.Viewmodels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Windows.ApplicationModel.DataTransfer;
using CommunityToolkit.WinUI.UI.Controls;
using Windows.System;

namespace HNReader.WinUI.Controls;

public sealed partial class StoriesPageControl : UserControl
{
    private PageViewModel? _currentViewModel;

    public StoriesPageControl()
    {
        InitializeComponent();

        StoriesList.SelectionChanged += StoriesList_SelectionChanged;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (_currentViewModel != null)
        {
            _currentViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (args.NewValue is PageViewModel vm)
        {
            _currentViewModel = vm;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
        else
        {
            _currentViewModel = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
    }

    private ILogger? ResolveLogger() => (Application.Current as App)?.Services.GetService<ILogger>();

    private async Task ExecuteUiActionSafelyAsync(Func<Task> action, string operationName)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ResolveLogger()?.LogError("StoriesPageControl", $"Error during {operationName}", ex);
        }
    }

    private async void OnLoadMoreClicked(object sender, RoutedEventArgs e)
    {
        await ExecuteUiActionSafelyAsync(async () =>
        {
            if (DataContext is not PageViewModel vm) return;
            if (!vm.LoadMoreStoriesCommand.CanExecute(null)) return;

            // Remember the visible count so we can find the first newly-loaded
            // item in the same collection the ListView is rendering.
            var filteredStoriesCountBefore = vm.FilteredStories.Count;

            await vm.LoadMoreStoriesCommand.ExecuteAsync(null);

            await RunOnStoriesListDispatcherAsync(() => StoriesList.UpdateLayout());

            if (vm.FilteredStories.Count > filteredStoriesCountBefore)
            {
                var firstNewStory = vm.FilteredStories[filteredStoriesCountBefore];
                StoriesList.ScrollIntoView(firstNewStory, ScrollIntoViewAlignment.Leading);
                await RunOnStoriesListDispatcherAsync(() => StoriesList.UpdateLayout());
            }
            else if (vm.IsAtEnd)
            {
                await RunOnStoriesListDispatcherAsync(() =>
                {
                    LoadMoreButton.StartBringIntoView(new BringIntoViewOptions
                    {
                        AnimationDesired = true
                    });
                });
            }
        }, "load more stories");
    }

    private Task RunOnStoriesListDispatcherAsync(Action action)
    {
        var dispatcherQueue = StoriesList.DispatcherQueue;
        if (dispatcherQueue == null)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }))
        {
            try
            {
                action();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }

        return tcs.Task;
    }

    private void StoriesList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is PageViewModel vm)
        {
            vm.SelectedStory = StoriesList.SelectedItem as Story;
        }
    }

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
            ResolveLogger()?.LogError("StoriesPageControl", "Exception in OnToggleWebCommentCollapseClicked", ex);
            throw;
        }
    }

    private async void OnCopyLinkClicked(object sender, RoutedEventArgs e)
    {
        await ExecuteUiActionSafelyAsync(async () =>
        {
            if (DataContext is not PageViewModel vm || vm.SelectedStory?.Url == null) return;

            var dataPackage = new DataPackage();
            dataPackage.SetText(vm.SelectedStory.Url);
            Clipboard.SetContent(dataPackage);

            await vm.ShowCopyFeedbackAsync("Link copied!");
        }, "copying link");
    }

    private async void OnCopyTitleClicked(object sender, RoutedEventArgs e)
    {
        await ExecuteUiActionSafelyAsync(async () =>
        {
            if (DataContext is not PageViewModel vm || vm.SelectedStory?.Title == null) return;

            var dataPackage = new DataPackage();
            dataPackage.SetText(vm.SelectedStory.Title);
            Clipboard.SetContent(dataPackage);

            await vm.ShowCopyFeedbackAsync("Title copied!");
        }, "copying title");
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
            ResolveLogger()?.LogError("StoriesPageControl", "Error sharing", ex);
        }
    }

    private async void OnViewOnHnClicked(object sender, RoutedEventArgs e)
    {
        await ExecuteUiActionSafelyAsync(async () =>
        {
            if (DataContext is not PageViewModel vm || vm.SelectedStory == null) return;

            var hnUrl = $"https://news.ycombinator.com/item?id={vm.SelectedStory.Id}";
            await Launcher.LaunchUriAsync(new Uri(hnUrl));
        }, "opening story on Hacker News");
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
        await ExecuteUiActionSafelyAsync(async () =>
        {
            await TryLaunchAsync(e.Link);
        }, "opening comment link");
    }

    private async void OnStoryMarkdownLinkClicked(object sender, LinkClickedEventArgs e)
    {
        await ExecuteUiActionSafelyAsync(async () =>
        {
            await TryLaunchAsync(e.Link);
        }, "opening story link");
    }

    private static async Task<bool> TryLaunchAsync(string? link)
    {
        if (string.IsNullOrWhiteSpace(link)) return false;

        link = WebUtility.HtmlDecode(link);

        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
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
}
