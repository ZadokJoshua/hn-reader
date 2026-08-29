using System;
using System.ComponentModel;
using System.Net;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using HNReader.Avalonia.Services;
using HNReader.Core.Models;
using HNReader.Core.Services.Logging;
using HNReader.Core.Viewmodels;
using Microsoft.Extensions.DependencyInjection;

namespace HNReader.Avalonia.Controls;

public partial class StoriesPageControl : UserControl
{
    private PageViewModel? _currentViewModel;

    public StoriesPageControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
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
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
    }

    private ILogger? ResolveLogger() => (global::Avalonia.Application.Current as App)?.Services.GetService<ILogger>();

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

    private async void OnLoadMoreClicked(object? sender, RoutedEventArgs e)
    {
        await ExecuteUiActionSafelyAsync(async () =>
        {
            if (DataContext is not PageViewModel vm) return;
            if (!vm.LoadMoreStoriesCommand.CanExecute(null)) return;

            await vm.LoadMoreStoriesCommand.ExecuteAsync(null);
        }, "load more stories");
    }

    private void OnToggleWebCommentCollapseClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Control fe && fe.Tag is WebCommentNode node)
            {
                PageViewModel.ToggleWebCommentCollapse(node);
            }
        }
        catch (Exception ex)
        {
            ResolveLogger()?.LogError("StoriesPageControl", "Exception in OnToggleWebCommentCollapseClicked", ex);
        }
    }

    private async void OnCopyLinkClicked(object? sender, RoutedEventArgs e)
    {
        await ExecuteUiActionSafelyAsync(async () =>
        {
            if (DataContext is not PageViewModel vm || vm.SelectedStory?.Url == null) return;

            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(vm.SelectedStory.Url);
            }

            await vm.ShowCopyFeedbackAsync("Link copied!");
        }, "copying link");
    }

    private async void OnCopyTitleClicked(object? sender, RoutedEventArgs e)
    {
        await ExecuteUiActionSafelyAsync(async () =>
        {
            if (DataContext is not PageViewModel vm || vm.SelectedStory?.Title == null) return;

            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(vm.SelectedStory.Title);
            }

            await vm.ShowCopyFeedbackAsync("Title copied!");
        }, "copying title");
    }

    private async void OnViewOnHnClicked(object? sender, RoutedEventArgs e)
    {
        await ExecuteUiActionSafelyAsync(() =>
        {
            if (DataContext is not PageViewModel vm || vm.SelectedStory == null) return Task.CompletedTask;

            var hnUrl = $"https://news.ycombinator.com/item?id={vm.SelectedStory.Id}";
            UrlOpener.TryOpen(hnUrl);
            return Task.CompletedTask;
        }, "opening story on Hacker News");
    }

    private void OnOpenStoryUrlClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not PageViewModel vm || vm.SelectedStory?.Url == null) return;
            TryLaunch(vm.SelectedStory.Url);
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
