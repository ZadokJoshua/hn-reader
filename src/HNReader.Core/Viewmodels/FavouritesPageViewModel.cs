using CommunityToolkit.Mvvm.ComponentModel;
using HNReader.Core.Enums;
using HNReader.Core.Interfaces;
using HNReader.Core.Models;
using HNReader.Core.Services;

namespace HNReader.Core.Viewmodels;

public partial class FavouritesPageViewModel : PageViewModel
{
    [ObservableProperty]
    private bool _showFavoritesEmptyState;

    partial void OnShowFavoritesEmptyStateChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowListEmptyState));
    }

    public override bool ShowListEmptyState => ShowFavoritesEmptyState;

    public override string EmptyStateTitle => "No favourites yet";

    public override string EmptyStateDescription => "Tap the heart icon on any story to save it here for quick access. Browse Top, New, or Best stories to start building your list.";

    public override string EmptyStateGlyph => "\uE734";

    public FavouritesPageViewModel(HNClient client, IFavoritesService favoritesService, ISettingsService settingsService, HNWebClient webClient, IContentScraperService contentScraperService, CopilotCliService copilotCliService, IVaultFileService vaultFileService)
        : base(client, favoritesService, StoryType.Top, contentScraperService, settingsService, webClient, copilotCliService, vaultFileService)
    {
        PageTitle = "Favourites";
        FavoritesService.FavoritesChanged += OnFavoritesChanged;
    }

    public override async Task PopulateListAsync()
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;
        IsDataVisible = false;
        HasMoreItems = false;
        UpdateEmptyState();

        try
        {
            // Get favorite IDs, then fetch fresh data from API
            var favoriteIds = await FavoritesService.GetAllIdsAsync();
            Stories.Clear();
            
            if (favoriteIds.Count > 0)
            {
                var tasks = favoriteIds.Select(id => Client.GetItemAsync<Story>(id));
                var stories = await Task.WhenAll(tasks);
                
                foreach (var story in stories.Where(s => s != null))
                {
                    story!.IsFavorite = true;
                    Stories.Add(story);
                }
            }

            ApplySearchFilter();
            IsDataVisible = Stories.Count > 0;
            UpdateEmptyState();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading favourites: {ex.Message}");
            HasError = true;
            ErrorMessage = "There was an error loading favourites.";
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
        // No pagination for favourites
        return Task.CompletedTask;
    }

    private async void OnFavoritesChanged(object? sender, EventArgs e)
    {
        await PopulateListAsync();
    }

    private void UpdateEmptyState()
    {
        ShowFavoritesEmptyState = !IsLoading && !HasError && Stories.Count == 0;
    }
}
