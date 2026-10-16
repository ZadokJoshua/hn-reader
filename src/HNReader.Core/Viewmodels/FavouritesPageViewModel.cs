using CommunityToolkit.Mvvm.ComponentModel;
using HNReader.Core.Enums;
using HNReader.Core.Helpers;
using HNReader.Core.Interfaces;
using HNReader.Core.Models;
using HNReader.Core.Services;
using HNReader.Core.Services.Logging;
using System;

namespace HNReader.Core.Viewmodels;

public partial class FavouritesPageViewModel : PageViewModel, IDisposable
{
    private bool _isDisposed;

    [ObservableProperty]
    private bool _showFavoritesEmptyState;

    partial void OnShowFavoritesEmptyStateChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowListEmptyState));
    }

    public override bool ShowListEmptyState => ShowFavoritesEmptyState;

    public override bool ShowLoadMoreFooter => false;

    /// <summary>
    /// The one page where search is meaningful: this list is complete and local, so
    /// filtering it cannot hide a match that simply has not been fetched yet.
    /// </summary>
    public override bool IsSearchEnabled => true;

    public override string EmptyStateTitle => "No favourites yet";

    public override string EmptyStateDescription => "Tap the heart icon on any story to save it here for quick access. Browse Top, New, or Best stories to start building your list.";

    public override string EmptyStateGlyph => "\uE734";

    public FavouritesPageViewModel(HNClient client, Lazy<IFavoritesService> favoritesService, HNWebClient webClient, ILogger? logger = null)
        : base(client, favoritesService, StoryType.Top, webClient, logger)
    {
        PageTitle = "Favourites";
        FavoritesService.FavoritesChanged += OnFavoritesChanged;
    }

    public override async Task PopulateListAsync()
    {
        IsLoading = true;
        HasError = false;
        IsOfflineError = false;
        PartialLoadMessage = string.Empty;
        ErrorMessage = string.Empty;
        IsDataVisible = false;
        HasMoreItems = false;
        UpdateEmptyState();

        try
        {
            var favoriteIds = await FavoritesService.GetAllIdsAsync();
            Stories.Clear();

            if (favoriteIds.Count > 0)
            {
                // One unreachable item must not hide every other favourite, so each fetch
                // reports its own failure instead of faulting the whole Task.WhenAll.
                Exception? firstFailure = null;
                var failed = 0;
                var tasks = favoriteIds.Select(async id =>
                {
                    try
                    {
                        return await Client.GetItemAsync<Story>(id);
                    }
                    catch (Exception ex)
                    {
                        firstFailure ??= ex;
                        Interlocked.Increment(ref failed);
                        return null;
                    }
                });
                var stories = await Task.WhenAll(tasks);

                foreach (var story in stories.Where(s => s != null))
                {
                    story!.IsFavorite = true;
                    Stories.Add(story);
                }

                if (failed > 0 && Stories.Count == 0 && firstFailure is not null)
                {
                    // Nothing could be loaded: that is a real error (usually offline), not a partial one.
                    throw firstFailure;
                }

                if (failed > 0)
                {
                    PartialLoadMessage = failed == 1
                        ? "1 favourite couldn't be loaded right now. Refresh to try again."
                        : $"{failed} favourites couldn't be loaded right now. Refresh to try again.";
                }
            }

            ApplySearchFilter();
            IsDataVisible = Stories.Count > 0;
            UpdateEmptyState();
        }
        catch (Exception ex)
        {
            Logger?.LogError("FavouritesPageViewModel", "Error loading favourites", ex);
            var friendly = ErrorMessageHelper.Describe(ex);
            HasError = true;
            IsOfflineError = friendly.IsOffline;
            ErrorTitle = friendly.IsOffline ? "You're offline" : "Couldn't load your favourites";
            ErrorMessage = friendly.Message;
            UpdateEmptyState();
        }
        finally
        {
            IsLoading = false;
            UpdateEmptyState();
        }
    }

    protected override Task LoadMoreStoriesAsync()
    {
        return Task.CompletedTask;
    }

    private async void OnFavoritesChanged(object? sender, EventArgs e)
    {
        if (_isDisposed) return;

        try
        {
            await PopulateListAsync();
        }
        catch (Exception ex)
        {
            Logger?.LogError("FavouritesPageViewModel", "Error refreshing favourites after change event", ex);
        }
    }

    private void UpdateEmptyState()
    {
        ShowFavoritesEmptyState = !IsLoading && !HasError && Stories.Count == 0;
        NotifyStoryCountDependents();
    }

    /// <summary>
    /// Re-publishes the properties on this page that are derived from Stories.Count.
    /// </summary>
    /// <remarks>
    /// This page fills its list with Stories.Clear() followed by Stories.Add(), which
    /// mutates the existing collection. That raises neither PageViewModel's
    /// OnStoriesChanged (which only fires when the collection itself is replaced) nor
    /// its private pagination notifier, so HasStories kept the false value published
    /// during the reset and the search box stayed disabled no matter how many
    /// favourites loaded.
    ///
    /// Deliberately local to this page rather than a change to PageViewModel: the base
    /// serves every other feed page, none of which have this problem, and widening the
    /// shared notification path to fix one page risks all of them. Called from
    /// UpdateEmptyState, which already runs at every
    /// point where the count can have changed.
    /// </remarks>
    private void NotifyStoryCountDependents()
    {
        OnPropertyChanged(nameof(HasStories));
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        FavoritesService.FavoritesChanged -= OnFavoritesChanged;
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
