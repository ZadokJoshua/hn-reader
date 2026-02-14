using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HNReader.Core.Enums;
using HNReader.Core.Helpers;
using HNReader.Core.Interfaces;
using HNReader.Core.Models;
using HNReader.Core.Services;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace HNReader.Core.Viewmodels;

/// <summary>
/// Base class for story-based pages (Top, New, Best, Ask, Show, Favorites).
/// Provides common functionality for loading stories, pagination, and comments.
/// </summary>
public abstract partial class PageViewModel : BaseViewModel
{
    private readonly HNClient _client;
    private readonly HNWebClient _webClient;
    private readonly IFavoritesService _favoritesService;
    private readonly ISettingsService? _settingsService;
    private readonly CopilotCliService? _copilotCliService;
    private readonly StoryType _itemType;
    private readonly IContentScraperService _contentScraperService;
    private readonly IVaultFileService _vaultFileService;
    private bool _suppressSelectedStoryReset;

    // In-memory comment cache shared across all PageViewModel instances.
    // Key: story ID, Value: built comment tree roots.
    // ConcurrentDictionary provides lock-free reads and thread-safe writes.
    private static readonly ConcurrentDictionary<int, List<WebCommentNode>> _commentCache = new();
    private const int MaxCachedStories = 50;

    // In-memory AI insight cache shared across all PageViewModel instances.
    // Key: story ID, Value: cached insight with panel open state.
    private static readonly ConcurrentDictionary<int, CachedStoryInsight> _insightCache = new();
    private const int MaxCachedInsights = 50;

    protected IFavoritesService FavoritesService => _favoritesService;
    protected HNClient Client => _client;

    // Pagination
    private int _currentPage = 0;
    private int PageSize => _settingsService?.StoryLimit ?? 20;
    private bool _hasMoreItems = true;
    public bool HasMoreItems
    {
        get => _hasMoreItems;
        protected set
        {
            if (_hasMoreItems != value)
            {
                _hasMoreItems = value;
                OnPropertyChanged(nameof(HasMoreItems));
                OnPropertyChanged(nameof(ShowLoadMoreButton));
                OnPropertyChanged(nameof(LoadMoreFooterVisible));
            }
        }
    }

    public bool ShowLoadMoreButton => HasMoreItems && string.IsNullOrWhiteSpace(SearchText);

    public bool LoadMoreFooterVisible => IsLoadingMore || ShowLoadMoreButton;

    // Only show Load More footer if there are stories and no error
    public bool ShowLoadMoreFooter => LoadMoreFooterVisible && !HasError && Stories.Count > 0;

    // Enable search and refresh only when there are stories
    public bool HasStories => Stories.Count > 0 && !HasError;

    [ObservableProperty]
    private ObservableCollection<Story> _stories = [];

    partial void OnStoriesChanged(ObservableCollection<Story> value)
    {
        OnPropertyChanged(nameof(HasStories));
        OnPropertyChanged(nameof(ShowLoadMoreFooter));
    }

    [ObservableProperty]
    private ObservableCollection<Story> _filteredStories = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoadingMore;

    partial void OnIsLoadingMoreChanged(bool value)
    {
        OnPropertyChanged(nameof(LoadMoreFooterVisible));
    }

    [ObservableProperty]
    private string _pageTitle = string.Empty;

    [ObservableProperty]
    private Story? _selectedStory;

    // Error state
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    partial void OnHasErrorChanged(bool value)
    {
        OnPropertyChanged(nameof(HasStories));
        OnPropertyChanged(nameof(ShowLoadMoreFooter));
    }

    [ObservableProperty]
    private bool _isDataVisible;

    [ObservableProperty]
    private string _searchText = string.Empty;

    // Web-based comments (faster approach)
    [ObservableProperty]
    private ObservableCollection<WebCommentNode> _webCommentNodes = [];

    private List<WebCommentNode>? _webCommentRoots;

    [ObservableProperty]
    private bool _areCommentsVisible;

    [ObservableProperty]
    private bool _isCommentsLoading;

    [ObservableProperty]
    private string _commentsErrorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasCommentsError;

    // Copy feedback
    [ObservableProperty]
    private string _copyFeedbackText = string.Empty;

    [ObservableProperty]
    private bool _showCopyFeedback;

    [ObservableProperty]
    private bool _isSelectedStoryFavorite;

    partial void OnIsSelectedStoryFavoriteChanged(bool value)
    {
        OnPropertyChanged(nameof(FavoriteButtonText));
        OnPropertyChanged(nameof(FavoriteButtonGlyph));
    }

    public string FavoriteButtonText => IsSelectedStoryFavorite ? "Remove Favourite" : "Add to Favourites";

    public string FavoriteButtonGlyph => IsSelectedStoryFavorite ? "\uEB52" : "\uEB51";

    public virtual bool ShowListEmptyState => false;

    public virtual string EmptyStateTitle => string.Empty;

    public virtual string EmptyStateDescription => string.Empty;

    public virtual string EmptyStateGlyph => "\uE734";

    public bool CommentsContentVisible => !IsCommentsLoading && !HasCommentsError;

    // Show web comments when available (preferred)
    public bool ShowWebComments => CommentsContentVisible && WebCommentNodes.Count > 0;

    public bool ShowNoCommentsMessage => CommentsContentVisible && AreCommentsVisible && 
        WebCommentNodes.Count == 0;

    // HN URL for selected story
    public string? SelectedStoryHnUrl => SelectedStory != null ? $"https://news.ycombinator.com/item?id={SelectedStory.Id}" : null;

    // AI Insights
    [ObservableProperty]
    private string _insightText = string.Empty;

    [ObservableProperty]
    private bool _isInsightLoading;

    [ObservableProperty]
    private bool _hasInsight;

    [ObservableProperty]
    private bool _hasInsightError;

    [ObservableProperty]
    private string _insightErrorMessage = string.Empty;

    [ObservableProperty]
    private string _insightProgressMessage = string.Empty;

    /// <summary>
    /// Controls the AI insights panel visibility from the ViewModel.
    /// Synced with code-behind panel state.
    /// </summary>
    [ObservableProperty]
    private bool _isInsightsPanelOpen;

    /// <summary>
    /// Whether the current story has a cached insight available.
    /// </summary>
    public bool HasCachedInsight => SelectedStory != null && _insightCache.ContainsKey(SelectedStory.Id);

    /// <summary>
    /// Whether to show the Generate Insights button.
    /// Hidden when insight is already generated or cached.
    /// </summary>
    public bool ShowGenerateInsightButton => !HasInsight && !HasCachedInsight && !IsInsightLoading;

    protected PageViewModel(HNClient client, IFavoritesService favoritesService, StoryType itemType, IContentScraperService contentScraperService, ISettingsService settingsService, HNWebClient webClient, CopilotCliService copilotCliService, IVaultFileService vaultFileService)
    {
        _client = client;
        _favoritesService = favoritesService;
        _itemType = itemType;
        _settingsService = settingsService;
        _webClient = webClient;
        _copilotCliService = copilotCliService;
        _contentScraperService = contentScraperService;
        _vaultFileService = vaultFileService;
    }

    public virtual async Task PopulateListAsync()
    {
        // Reset all state for fresh navigation
        ResetPageState();
        
        IsLoading = true;

        try
        {
            var stories = await _client.GetStoriesAsync(_itemType, limit: PageSize);
            
            foreach (var story in stories)
            {
                Stories.Add(story);
            }

            // Update favorite status for all loaded stories
            await UpdateFavoriteStatusForStoriesAsync();

            ApplySearchFilter();

            _currentPage = 1;
            HasMoreItems = stories.Count >= PageSize;
            IsDataVisible = Stories.Count > 0;
            OnPropertyChanged(nameof(HasStories));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading stories: {ex.Message}");
            HasError = true;
            ErrorMessage = "There was an error loading stories. Please check your connection and try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Resets all page state to prepare for fresh data load.
    /// Called at the start of navigation to ensure clean slate.
    /// </summary>
    private void ResetPageState()
    {
        // Reset detail view state
        SelectedStory = null;
        
        // Clear collections BEFORE the try block to ensure old data doesn't show on error
        Stories.Clear();
        FilteredStories.Clear();
        
        // Reset search
        SearchText = string.Empty;
        
        // Reset error state
        HasError = false;
        ErrorMessage = string.Empty;
        
        // Reset pagination
        _currentPage = 0;
        HasMoreItems = true;
        IsDataVisible = false;
        
        // Notify dependent properties
        OnPropertyChanged(nameof(HasStories));
        OnPropertyChanged(nameof(ShowLoadMoreFooter));
    }

    [RelayCommand]
    protected virtual async Task LoadMoreStoriesAsync()
    {
        if (IsLoadingMore || IsLoading || !HasMoreItems) return;

        IsLoadingMore = true;

        try
        {
            var offset = _currentPage * PageSize;
            var stories = await _client.GetStoriesAsync(_itemType, limit: PageSize, offset: offset);

            foreach (var story in stories)
            {
                Stories.Add(story);
            }

            // Update favorite status for newly loaded stories
            foreach (var story in stories)
            {
                story.IsFavorite = await _favoritesService.ExistsAsync(story.Id);
            }

            ApplySearchFilter();

            _currentPage++;
            HasMoreItems = stories.Count >= PageSize;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading more stories: {ex.Message}");
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplySearchFilter();
        OnPropertyChanged(nameof(ShowLoadMoreButton));
        OnPropertyChanged(nameof(LoadMoreFooterVisible));
    }

    protected void ApplySearchFilter()
    {
        var term = SearchText?.Trim() ?? string.Empty;
        var hasFilter = !string.IsNullOrWhiteSpace(term);
        var previouslySelectedStory = SelectedStory;
        var shouldPreserveSelection = previouslySelectedStory != null && (!hasFilter || StoryMatchesFilter(previouslySelectedStory, term));
        Story? selectionToRestore = null;

        if (shouldPreserveSelection)
        {
            _suppressSelectedStoryReset = true;
        }

        try
        {
            FilteredStories.Clear();
            foreach (var story in Stories)
            {
                if (hasFilter && !StoryMatchesFilter(story, term))
                {
                    continue;
                }

                FilteredStories.Add(story);
                if (selectionToRestore == null && shouldPreserveSelection && previouslySelectedStory != null && ReferenceEquals(story, previouslySelectedStory))
                {
                    selectionToRestore = story;
                }
            }

            if (selectionToRestore != null && !ReferenceEquals(SelectedStory, selectionToRestore))
            {
                SelectedStory = selectionToRestore;
            }
        }
        finally
        {
            _suppressSelectedStoryReset = false;
        }
    }

    [RelayCommand]
    private async Task RetryAsync()
    {
        await PopulateListAsync();
    }

    public async Task ShowCopyFeedbackAsync(string message)
    {
        CopyFeedbackText = message;
        ShowCopyFeedback = true;
        await Task.Delay(2000);
        ShowCopyFeedback = false;
    }

    public string CommentsButtonText => AreCommentsVisible ? "Hide Comments" : "View Comments";

    partial void OnAreCommentsVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(CommentsButtonText));
        OnPropertyChanged(nameof(ShowNoCommentsMessage));
    }

    partial void OnIsCommentsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CommentsContentVisible));
        OnPropertyChanged(nameof(ShowWebComments));
        OnPropertyChanged(nameof(ShowNoCommentsMessage));
    }

    partial void OnHasCommentsErrorChanged(bool value)
    {
        OnPropertyChanged(nameof(CommentsContentVisible));
        OnPropertyChanged(nameof(ShowWebComments));
        OnPropertyChanged(nameof(ShowNoCommentsMessage));
    }

    partial void OnHasInsightChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowGenerateInsightButton));
    }

    partial void OnIsInsightLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowGenerateInsightButton));
    }

    /// <summary>
    /// When panel state changes for a story with insight, update the cache.
    /// </summary>
    partial void OnIsInsightsPanelOpenChanged(bool value)
    {
        // Update the cache entry for the current story's panel state
        if (SelectedStory != null && HasInsight && !string.IsNullOrEmpty(InsightText))
        {
            _insightCache[SelectedStory.Id] = new CachedStoryInsight(InsightText, value);
        }
    }

    partial void OnSelectedStoryChanged(Story? oldValue, Story? newValue)
    {
        if (_suppressSelectedStoryReset)
        {
            return;
        }

        // Save current insight state to cache for the old story (if insight was generated)
        if (oldValue != null && HasInsight && !string.IsNullOrEmpty(InsightText))
        {
            _insightCache[oldValue.Id] = new CachedStoryInsight(InsightText, IsInsightsPanelOpen);
            EvictOldInsightsIfNeeded();
        }

        // Only clear collections if they have items to avoid unnecessary CollectionChanged events
        if (WebCommentNodes.Count > 0)
            WebCommentNodes.Clear();
        _webCommentRoots = null;
        AreCommentsVisible = false;
        IsCommentsLoading = false;
        HasCommentsError = false;
        CommentsErrorMessage = string.Empty;
        ShowCopyFeedback = false;
        
        // Reset insight state
        InsightText = string.Empty;
        HasInsight = false;
        HasInsightError = false;
        InsightErrorMessage = string.Empty;
        InsightProgressMessage = string.Empty;
        IsInsightLoading = false;

        // Restore cached insight for the new story if available
        if (newValue != null && _insightCache.TryGetValue(newValue.Id, out var cachedInsight))
        {
            InsightText = cachedInsight.InsightText;
            HasInsight = true;
            IsInsightsPanelOpen = cachedInsight.IsPanelOpen;
        }
        else
        {
            // No cached insight: close the panel for this story
            IsInsightsPanelOpen = false;
        }

        OnPropertyChanged(nameof(CommentsButtonText));
        OnPropertyChanged(nameof(ShowNoCommentsMessage));
        OnPropertyChanged(nameof(SelectedStoryHnUrl));
        OnPropertyChanged(nameof(HasCachedInsight));
        OnPropertyChanged(nameof(ShowGenerateInsightButton));
        ToggleFavoriteCommand.NotifyCanExecuteChanged();
        _ = UpdateSelectedStoryFavoriteStateAsync();
    }

    /// <summary>
    /// Evicts old cached insights if the cache exceeds the maximum size.
    /// </summary>
    private static void EvictOldInsightsIfNeeded()
    {
        if (_insightCache.Count > MaxCachedInsights)
        {
            // Remove oldest entries (FIFO approximation using first keys)
            var keysToRemove = _insightCache.Keys.Take(_insightCache.Count - MaxCachedInsights).ToList();
            foreach (var key in keysToRemove)
            {
                _insightCache.TryRemove(key, out _);
            }
        }
    }

    [RelayCommand]
    private async Task ToggleCommentsAsync()
    {
        if (SelectedStory == null) return;

        if (AreCommentsVisible)
        {
            AreCommentsVisible = false;
            return;
        }

        // Check if we already have comments loaded
        if (WebCommentNodes.Count > 0)
        {
            AreCommentsVisible = true;
            return;
        }

        try
        {
            IsCommentsLoading = true;
            AreCommentsVisible = true;
            HasCommentsError = false;
            CommentsErrorMessage = string.Empty;

            // Use the faster web-based approach if available
            if (_webClient != null)
            {
                await LoadCommentsFromWebAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading comments: {ex.Message}");
            HasCommentsError = true;
            CommentsErrorMessage = "There was an error loading comments. Please try again.";
        }
        finally
        {
            IsCommentsLoading = false;
            OnPropertyChanged(nameof(CommentsButtonText));
        }
    }

    /// <summary>
    /// Loads comments using the faster web scraping approach.
    /// Uses an in-memory ConcurrentDictionary cache to avoid re-fetching
    /// comments for stories that have already been loaded in this session.
    /// </summary>
    private async Task LoadCommentsFromWebAsync()
    {
        if (SelectedStory == null || _webClient == null) return;

        var storyId = SelectedStory.Id;

        // Check cache first
        if (_commentCache.TryGetValue(storyId, out var cached))
        {
            _webCommentRoots = cached;
        }
        else
        {
            var webComments = await _webClient.GetCommentsFromWebAsync(storyId);

            // Build tree on a background thread to avoid blocking the UI.
            // WebCommentNode constructors call HtmlContentHelper.ToPlainText/ToMarkdown
            // which perform synchronous HTML parsing for every comment.
            _webCommentRoots = await Task.Run(() => CommentTreeBuilder.BuildTree(webComments));

            // Evict oldest entries if the cache grows too large
            if (_commentCache.Count >= MaxCachedStories)
            {
                _commentCache.Clear();
            }

            _commentCache.TryAdd(storyId, _webCommentRoots);
        }

        // Replace the collection in one shot so the UI receives a single PropertyChanged
        // notification instead of N CollectionChanged events from individual Add() calls.
        WebCommentNodes = new ObservableCollection<WebCommentNode>(_webCommentRoots);

        OnPropertyChanged(nameof(ShowWebComments));
        OnPropertyChanged(nameof(ShowNoCommentsMessage));
    }

    /// <summary>
    /// Toggles collapse state for a web comment.
    /// Uses property change notification - no need to rebuild the list.
    /// </summary>
    public static void ToggleWebCommentCollapse(WebCommentNode node) => node.ToggleCollapsed();

    private bool CanToggleFavorite() => SelectedStory != null;

    [RelayCommand(CanExecute = nameof(CanToggleFavorite))]
    private async Task ToggleFavoriteAsync()
    {
        if (SelectedStory == null) return;

        if (IsSelectedStoryFavorite)
        {
            await _favoritesService.RemoveAsync(SelectedStory.Id);
            IsSelectedStoryFavorite = false;
        }
        else
        {
            await _favoritesService.AddOrUpdateAsync(SelectedStory);
            IsSelectedStoryFavorite = true;
        }

        // Update the favorite status in the story - it will notify the UI automatically
        if (SelectedStory != null)
        {
            SelectedStory.IsFavorite = IsSelectedStoryFavorite;
        }
    }

    private async Task UpdateSelectedStoryFavoriteStateAsync()
    {
        if (SelectedStory == null)
        {
            IsSelectedStoryFavorite = false;
            return;
        }

        IsSelectedStoryFavorite = await _favoritesService.ExistsAsync(SelectedStory.Id);
        SelectedStory.IsFavorite = IsSelectedStoryFavorite;
    }

    /// <summary>
    /// Updates the IsFavorite property for all stories in the list.
    /// </summary>
    private async Task UpdateFavoriteStatusForStoriesAsync()
    {
        foreach (var story in Stories)
        {
            story.IsFavorite = await _favoritesService.ExistsAsync(story.Id);
        }
    }

    private static bool StoryMatchesFilter(Story story, string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return true;
        }

        return (!string.IsNullOrEmpty(story.Title) && story.Title.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrEmpty(story.By) && story.By.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrEmpty(story.RootDomain) && story.RootDomain.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    // ── AI Insights ─────────────────────────────────────────────────────

    [RelayCommand]
    private async Task GenerateInsightAsync()
    {
        var selectedStory = SelectedStory;
        if (selectedStory == null || _copilotCliService == null) return;

        IsInsightLoading = true;
        HasInsight = false;
        HasInsightError = false;
        InsightErrorMessage = string.Empty;
        InsightText = string.Empty;
        InsightProgressMessage = "Preparing...";
        OnPropertyChanged(nameof(ShowGenerateInsightButton));

        // Create progress handler for UI updates
        var progress = new Progress<InsightGenerationProgress>(OnInsightProgressReported);

        try
        {
            string storyContent;
            var storyTitle = selectedStory.Title ?? string.Empty;

            InsightProgressMessage = "Scraping article content...";

            if (IsAskOrShowStory(storyTitle))
            {
                storyContent = selectedStory.Text ?? string.Empty;
            }
            else if (!string.IsNullOrWhiteSpace(selectedStory.Url))
            {
                storyContent = await _contentScraperService.GetPlainTextAsync(selectedStory.Url);
            }
            else
            {
                storyContent = string.Empty;
            }

            InsightProgressMessage = "Loading comments...";

            var storyComments = await _webClient.GetCommentsFromWebAsync(selectedStory.Id);
            var commentNodes = CommentTreeBuilder.BuildTree(storyComments);
            var storyBy = string.IsNullOrWhiteSpace(selectedStory.By) ? "unknown" : selectedStory.By;
            var storyMarkdownStr = MarkdownGenerator.BuildStoryMarkdown(new StoryData(selectedStory.Id, storyTitle, storyBy, storyContent), commentNodes);
            
            InsightProgressMessage = "Saving to knowledge vault...";
            await _vaultFileService.SaveStoryMarkdownAsync(selectedStory.Id, storyMarkdownStr);

            var result = await _copilotCliService.GenerateStoryInsightAsync(selectedStory.Id, progress);

            InsightText = result;
            HasInsight = true;
            IsInsightsPanelOpen = true;

            // Cache the insight for this story
            _insightCache[selectedStory.Id] = new CachedStoryInsight(result, true);
            EvictOldInsightsIfNeeded();
        }
        catch (OperationCanceledException)
        {
            // User cancelled — no action needed
            InsightProgressMessage = string.Empty;
        }
        catch (Exception ex)
        {
            HasInsightError = true;
            InsightErrorMessage = ex.Message;
            InsightProgressMessage = string.Empty;
        }
        finally
        {
            IsInsightLoading = false;
            OnPropertyChanged(nameof(HasCachedInsight));
            OnPropertyChanged(nameof(ShowGenerateInsightButton));
        }
    }

    /// <summary>
    /// Handles progress updates from insight generation.
    /// </summary>
    private void OnInsightProgressReported(InsightGenerationProgress progressUpdate)
    {
        InsightProgressMessage = progressUpdate.Message;

        if (progressUpdate.HasError)
        {
            HasInsightError = true;
            InsightErrorMessage = progressUpdate.ErrorMessage ?? "Unknown error";
        }
    }

    // method to check if a story is ask, hn, or show type (i.e. has no external URL) using the story title string as the method arguement
    public static bool IsAskOrShowStory(string title)
    {
        var lowerTitle = title.ToLowerInvariant();
        return lowerTitle.StartsWith("ask hn") || lowerTitle.StartsWith("show hn");
    }
}
