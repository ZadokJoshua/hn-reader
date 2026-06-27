using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HNReader.Core.Enums;
using HNReader.Core.Helpers;
using HNReader.Core.Interfaces;
using HNReader.Core.Models;
using HNReader.Core.Services;
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
    private readonly Lazy<IFavoritesService> _favoritesService;
    private readonly StoryType _itemType;
    private bool _suppressSelectedStoryReset;

    private CancellationTokenSource? _commentsLoadCts;

    private static readonly LRUCache<int, List<WebCommentNode>> _commentCache = new(maxCapacity: 200);

    protected IFavoritesService FavoritesService => _favoritesService.Value;
    protected HNClient Client => _client;

    // Pagination
    private int _currentPage = 0;
    private const int PageSize = 20;
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
                OnPropertyChanged(nameof(IsAtEnd));
                OnPropertyChanged(nameof(LoadMoreButtonText));
                OnPropertyChanged(nameof(IsLoadMoreEnabled));
            }
        }
    }

    public bool ShowLoadMoreButton => HasMoreItems && string.IsNullOrWhiteSpace(SearchText);

    public bool LoadMoreFooterVisible => IsLoadingMore || ShowLoadMoreButton || IsAtEnd;

    public virtual bool ShowLoadMoreFooter => LoadMoreFooterVisible && !HasError && Stories.Count > 0;

    public bool IsAtEnd => !HasMoreItems && Stories.Count > 0 && string.IsNullOrWhiteSpace(SearchText);

    public string LoadMoreButtonText => IsAtEnd ? "No more stories" : "Load more stories";

    public bool IsLoadMoreEnabled => !IsLoadingMore && !IsAtEnd && string.IsNullOrWhiteSpace(SearchText);

    public bool HasStories => Stories.Count > 0 && !HasError;

    [ObservableProperty]
    private ObservableCollection<Story> _stories = [];

    partial void OnStoriesChanged(ObservableCollection<Story> value)
    {
        OnPropertyChanged(nameof(HasStories));
        OnPropertyChanged(nameof(ShowLoadMoreFooter));
        OnPropertyChanged(nameof(IsAtEnd));
        OnPropertyChanged(nameof(LoadMoreButtonText));
        OnPropertyChanged(nameof(IsLoadMoreEnabled));
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
        OnPropertyChanged(nameof(IsLoadMoreEnabled));
    }

    [ObservableProperty]
    private string _pageTitle = string.Empty;

    [ObservableProperty]
    private Story? _selectedStory;

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

    [ObservableProperty]
    private bool _hasConfirmedNoComments;

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

    public bool ShowWebComments => CommentsContentVisible && WebCommentNodes.Count > 0;

    public bool ShowNoCommentsMessage => CommentsContentVisible && AreCommentsVisible && HasConfirmedNoComments &&
        WebCommentNodes.Count == 0;

    public string? SelectedStoryHnUrl => SelectedStory != null ? $"https://news.ycombinator.com/item?id={SelectedStory.Id}" : null;

    // ── Construction ─────────────────────────────────────────────────────

    protected PageViewModel(HNClient client, Lazy<IFavoritesService> favoritesService, StoryType itemType, HNWebClient webClient)
    {
        _client = client;
        _favoritesService = favoritesService;
        _itemType = itemType;
        _webClient = webClient;
    }

    public virtual async Task PopulateListAsync()
    {
        ResetPageState();

        IsLoading = true;

        try
        {
            // Intentionally NOT using ConfigureAwait(false) here: the await needs to
            // resume on the UI thread so the ObservableCollection<Story> mutations below
            // run on the dispatcher. WinUI 3 will deadlock if you update a bound
            // collection from a thread-pool thread.
            var stories = await _client.GetStoriesAsync(_itemType, limit: PageSize);

            foreach (var story in stories)
            {
                Stories.Add(story);
            }

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

    private void ResetPageState()
    {
        SelectedStory = null;

        Stories.Clear();
        FilteredStories.Clear();

        SearchText = string.Empty;

        HasError = false;
        ErrorMessage = string.Empty;

        _currentPage = 0;
        HasMoreItems = true;
        IsDataVisible = false;

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
            // Stay on the UI thread after await — see PopulateListAsync comment.
            var stories = await _client.GetStoriesAsync(_itemType, limit: PageSize, offset: offset);

            foreach (var story in stories)
            {
                Stories.Add(story);
            }

            var favTasks = stories.Select(async story =>
            {
                story.IsFavorite = await FavoritesService.ExistsAsync(story.Id).ConfigureAwait(false);
            });
            await Task.WhenAll(favTasks);

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

    partial void OnHasConfirmedNoCommentsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowNoCommentsMessage));
    }

    partial void OnSelectedStoryChanged(Story? oldValue, Story? newValue)
    {
        if (_suppressSelectedStoryReset)
        {
            return;
        }

        CancelCommentsLoad();

        if (WebCommentNodes.Count > 0)
            WebCommentNodes.Clear();
        _webCommentRoots = null;
        AreCommentsVisible = false;
        IsCommentsLoading = false;
        HasCommentsError = false;
        HasConfirmedNoComments = false;
        CommentsErrorMessage = string.Empty;
        ShowCopyFeedback = false;

        OnPropertyChanged(nameof(CommentsButtonText));
        OnPropertyChanged(nameof(ShowNoCommentsMessage));
        OnPropertyChanged(nameof(SelectedStoryHnUrl));
        ToggleFavoriteCommand.NotifyCanExecuteChanged();
        _ = UpdateSelectedStoryFavoriteStateAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task ToggleCommentsAsync()
    {
        if (SelectedStory == null) return;

        if (IsCommentsLoading)
        {
            CancelCommentsLoad();
            IsCommentsLoading = false;
            AreCommentsVisible = false;
            HasConfirmedNoComments = false;
            return;
        }

        if (AreCommentsVisible)
        {
            AreCommentsVisible = false;
            return;
        }

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
            HasConfirmedNoComments = false;
            CommentsErrorMessage = string.Empty;

            if (_webClient != null)
            {
                _commentsLoadCts = new CancellationTokenSource();
                await LoadCommentsFromWebAsync(_commentsLoadCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            AreCommentsVisible = false;
            HasConfirmedNoComments = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading comments: {ex.Message}");
            HasCommentsError = true;
            HasConfirmedNoComments = false;
            CommentsErrorMessage = "There was an error loading comments. Please try again.";
            AreCommentsVisible = false;
            _webCommentRoots = null;
            WebCommentNodes = [];
            OnPropertyChanged(nameof(ShowWebComments));
            OnPropertyChanged(nameof(ShowNoCommentsMessage));
        }
        finally
        {
            IsCommentsLoading = false;
            OnPropertyChanged(nameof(CommentsButtonText));
        }
    }

    private async Task LoadCommentsFromWebAsync(CancellationToken cancellationToken)
    {
        if (SelectedStory == null || _webClient == null) return;

        var storyId = SelectedStory.Id;

        cancellationToken.ThrowIfCancellationRequested();

        if (_commentCache.TryGetValue(storyId, out var cached) && cached != null)
        {
            _webCommentRoots = cached;
        }
        else
        {
            var webComments = await GetCommentsWithEmptyRetryAsync(SelectedStory, storyId, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // Tree build runs on a background thread. Each WebCommentNode ctor is
            // cheap — the expensive HTML→Markdown conversion is deferred via a
            // Lazy<string> and only fires when the comment is actually rendered.
            _webCommentRoots = await Task.Factory.StartNew(
                () => CommentTreeBuilder.BuildTree(webComments, cancellationToken),
                cancellationToken,
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);

            cancellationToken.ThrowIfCancellationRequested();

            if (_webCommentRoots.Count > 0)
            {
                _commentCache.Set(storyId, _webCommentRoots);
            }

            // The HN Firebase API sometimes returns 0 descendants for stories
            // (notably Ask HN posts). When that happens, get the real count from
            // the HTML page so the badge and any other UI bound to CommentCount
            // reflects reality. This is a fire-and-forget refresh — we don't
            // block the comment rendering on it.
            if (_webCommentRoots.Count > 0 && SelectedStory.Descendants is null or 0)
            {
                var accurateCount = await _webClient.GetAccurateCommentCountAsync(storyId, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (accurateCount > 0)
                {
                    SelectedStory.Descendants = accurateCount;
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        await LoadCommentsBatchedAsync(_webCommentRoots ?? [], cancellationToken);
    }

    private async Task<List<WebComment>> GetCommentsWithEmptyRetryAsync(Story story, int storyId, CancellationToken cancellationToken)
    {
        var webComments = await _webClient.GetCommentsFromWebAsync(storyId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (webComments.Count > 0)
        {
            return webComments;
        }

        if (story.CommentCount > 0)
        {
            webComments = await _webClient.GetCommentsFromWebAsync(storyId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (webComments.Count > 0)
            {
                return webComments;
            }

            throw new InvalidOperationException("Hacker News reported comments, but no comments could be loaded.");
        }

        var accurateCount = await _webClient.GetAccurateCommentCountAsync(storyId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (accurateCount > 0)
        {
            story.Descendants = accurateCount;

            webComments = await _webClient.GetCommentsFromWebAsync(storyId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (webComments.Count > 0)
            {
                return webComments;
            }

            throw new InvalidOperationException("Hacker News reported comments, but no comments could be parsed.");
        }

        HasConfirmedNoComments = true;
        return webComments;
    }

    private async Task LoadCommentsBatchedAsync(List<WebCommentNode> roots, CancellationToken cancellationToken)
    {
        const int batchSize = 15;

        if (roots == null || roots.Count == 0)
        {
            WebCommentNodes = [];
            OnPropertyChanged(nameof(ShowWebComments));
            OnPropertyChanged(nameof(ShowNoCommentsMessage));
            return;
        }

        WebCommentNodes = [];
        OnPropertyChanged(nameof(ShowWebComments));
        OnPropertyChanged(nameof(ShowNoCommentsMessage));

        for (int i = 0; i < roots.Count; i += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var end = Math.Min(i + batchSize, roots.Count);
            for (int j = i; j < end; j++)
            {
                WebCommentNodes.Add(roots[j]);
            }

            if (end < roots.Count)
            {
                // Yield to the dispatcher so the UI can render the batch
                // before the next one is appended. Task.Yield is free (no timer)
                // compared to Task.Delay(1).
                await Task.Yield();
            }
        }

        OnPropertyChanged(nameof(ShowWebComments));
        OnPropertyChanged(nameof(ShowNoCommentsMessage));
    }

    private void CancelCommentsLoad()
    {
        try
        {
            _commentsLoadCts?.Cancel();
        }
        catch
        {
        }
        finally
        {
            _commentsLoadCts?.Dispose();
            _commentsLoadCts = null;
        }
    }

    public static void ToggleWebCommentCollapse(WebCommentNode node)
    {
        if (node == null) return;

        node.ToggleCollapsed();
    }

    private void RefreshVisibleWebComments()
    {
        if (_webCommentRoots == null || _webCommentRoots.Count == 0)
        {
            WebCommentNodes = [];
            OnPropertyChanged(nameof(ShowWebComments));
            OnPropertyChanged(nameof(ShowNoCommentsMessage));
            return;
        }

        WebCommentNodes = new ObservableCollection<WebCommentNode>(_webCommentRoots);

        OnPropertyChanged(nameof(ShowWebComments));
        OnPropertyChanged(nameof(ShowNoCommentsMessage));
    }

    // ── Favorites ────────────────────────────────────────────────────────

    public WebCommentNode? FindCommentById(int commentId)
    {
        if (_webCommentRoots == null || commentId <= 0) return null;
        return FindNodeByIdRecursive(_webCommentRoots, commentId);
    }

    private static WebCommentNode? FindNodeByIdRecursive(IEnumerable<WebCommentNode> nodes, int commentId)
    {
        foreach (var node in nodes)
        {
            if (node.CommentId == commentId) return node;
            var childResult = FindNodeByIdRecursive(node.Children, commentId);
            if (childResult != null) return childResult;
        }
        return null;
    }

    /// <summary>
    /// Highlights a comment node temporarily so the user can find it in the tree.
    /// </summary>
    public async Task HighlightCommentAsync(WebCommentNode node, int durationMs = 3000)
    {
        if (node == null) return;

        if (!AreCommentsVisible) AreCommentsVisible = true;

        EnsureCommentVisible(node);

        node.IsHighlighted = true;
        await Task.Delay(durationMs);
        node.IsHighlighted = false;
    }

    private void EnsureCommentVisible(WebCommentNode target)
    {
        if (_webCommentRoots == null) return;
        ExpandPathTo(_webCommentRoots, target);
    }

    private static bool ExpandPathTo(IEnumerable<WebCommentNode> nodes, WebCommentNode target)
    {
        foreach (var node in nodes)
        {
            if (ReferenceEquals(node, target)) return true;

            if (node.Children.Count > 0 && ExpandPathTo(node.Children, target))
            {
                if (node.IsCollapsed) node.IsCollapsed = false;
                return true;
            }
        }
        return false;
    }

    private bool CanToggleFavorite() => SelectedStory != null;

    [RelayCommand(CanExecute = nameof(CanToggleFavorite))]
    private async Task ToggleFavoriteAsync()
    {
        if (SelectedStory == null) return;

        if (IsSelectedStoryFavorite)
        {
            await FavoritesService.RemoveAsync(SelectedStory.Id);
            IsSelectedStoryFavorite = false;
        }
        else
        {
            await FavoritesService.AddOrUpdateAsync(SelectedStory);
            IsSelectedStoryFavorite = true;
        }

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

        IsSelectedStoryFavorite = await FavoritesService.ExistsAsync(SelectedStory.Id);
        SelectedStory.IsFavorite = IsSelectedStoryFavorite;
    }

    private async Task UpdateFavoriteStatusForStoriesAsync()
    {
        var tasks = Stories.Select(async story =>
        {
            story.IsFavorite = await FavoritesService.ExistsAsync(story.Id);
        });
        await Task.WhenAll(tasks);
    }

    private static bool StoryMatchesFilter(Story story, string term)
    {
        if (string.IsNullOrWhiteSpace(term)) return true;

        return (!string.IsNullOrEmpty(story.Title) && story.Title.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrEmpty(story.By) && story.By.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrEmpty(story.RootDomain) && story.RootDomain.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
