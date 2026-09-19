using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HNReader.Core.Enums;
using HNReader.Core.Helpers;
using HNReader.Core.Interfaces;
using HNReader.Core.Models;
using HNReader.Core.Services;
using HNReader.Core.Services.Logging;
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
    private readonly ILogger? _logger;
    private bool _suppressSelectedStoryReset;

    private CancellationTokenSource? _commentsLoadCts;

    private static readonly LRUCache<int, List<WebCommentNode>> _commentCache = new(maxCapacity: 200);

    protected IFavoritesService FavoritesService => _favoritesService.Value;
    protected HNClient Client => _client;
    protected ILogger? Logger => _logger;

    // Pagination
    private int _nextOffset = 0;
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

    // These are all derived from Stories.Count, which changes via Stories.Add()
    // calls that don't themselves raise PropertyChanged for anything (and the
    // HasMoreItems setter above only notifies when its own value actually
    // flips). Call this after any batch of additions completes so bindings
    // depending on the story count/pagination state re-evaluate reliably,
    // regardless of whether HasMoreItems happened to change this time.
    private void NotifyPaginationPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasStories));
        OnPropertyChanged(nameof(ShowLoadMoreButton));
        OnPropertyChanged(nameof(LoadMoreFooterVisible));
        OnPropertyChanged(nameof(ShowLoadMoreFooter));
        OnPropertyChanged(nameof(IsAtEnd));
        OnPropertyChanged(nameof(LoadMoreButtonText));
        OnPropertyChanged(nameof(IsLoadMoreEnabled));
    }

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

    partial void OnIsLoadingMoreChanged(bool value) => NotifyPaginationPropertiesChanged();

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
    private bool _showCommentsEnd;

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

    protected PageViewModel(HNClient client, Lazy<IFavoritesService> favoritesService, StoryType itemType, HNWebClient webClient, ILogger? logger = null)
    {
        _client = client;
        _favoritesService = favoritesService;
        _itemType = itemType;
        _webClient = webClient;
        _logger = logger;
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
            var (stories, nextOffset) = await _client.GetStoriesPageAsync(_itemType, limit: PageSize);

            foreach (var story in stories)
            {
                Stories.Add(story);
            }

            await UpdateFavoriteStatusForStoriesAsync();

            ApplySearchFilter();

            _nextOffset = nextOffset;
            HasMoreItems = stories.Count >= PageSize;
            IsDataVisible = Stories.Count > 0;
            NotifyPaginationPropertiesChanged();
        }
        catch (Exception ex)
        {
            _logger?.LogError("PageViewModel", "Error loading stories", ex);
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

        _nextOffset = 0;
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
            // Stay on the UI thread after await — see PopulateListAsync comment.
            var (stories, nextOffset) = await _client.GetStoriesPageAsync(_itemType, limit: PageSize, offset: _nextOffset);

            foreach (var story in stories)
            {
                Stories.Add(story);
            }

            var favTasks = stories.Select(async story =>
            {
                story.IsFavorite = await FavoritesService.ExistsAsync(story.Id).ConfigureAwait(false);
            });
            await Task.WhenAll(favTasks);

            AppendToSearchFilter(stories);

            _nextOffset = nextOffset;
            HasMoreItems = stories.Count >= PageSize;
            NotifyPaginationPropertiesChanged();
        }
        catch (Exception ex)
        {
            _logger?.LogError("PageViewModel", "Error loading more stories", ex);
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplySearchFilter();
        NotifyPaginationPropertiesChanged();
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    /// <summary>
    /// Adds newly loaded stories to the filtered view, for the load-more path.
    /// <para>
    /// Load-more used to call <see cref="ApplySearchFilter"/>, which clears
    /// FilteredStories and rebuilds it. Because that is the collection bound to
    /// the list, clearing it made the list drop its selection and the two-way
    /// binding write null back into <see cref="SelectedStory"/>; restoring the
    /// selection a moment later then re-triggered the list's
    /// AutoScrollToSelectedItem (on by default), which yanked the viewport back
    /// up to the selected story every time a page loaded — right after the user
    /// had deliberately scrolled to the bottom.
    /// </para>
    /// <para>
    /// Appending produces the same result, since existing matches already sit in
    /// order at the head and new stories belong at the tail. Selection is never
    /// lost, so nothing re-triggers the scroll, and existing rows aren't
    /// needlessly rebuilt. No selection-preservation dance is needed here for
    /// the same reason.
    /// </para>
    /// </summary>
    protected void AppendToSearchFilter(IEnumerable<Story> newStories)
    {
        var term = SearchText?.Trim() ?? string.Empty;
        var hasFilter = !string.IsNullOrWhiteSpace(term);

        foreach (var story in newStories)
        {
            if (hasFilter && !StoryMatchesFilter(story, term))
            {
                continue;
            }

            FilteredStories.Add(story);
        }
    }

    /// <summary>
    /// Rebuilds the filtered view from scratch — for full reloads and search
    /// term changes, where resetting scroll position is the correct behaviour.
    /// Load-more deliberately uses <see cref="AppendToSearchFilter"/> instead.
    /// </summary>
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
        ShowCommentsEnd = false;
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
            _logger?.LogError("PageViewModel", "Error loading comments", ex);
            HasCommentsError = true;
            HasConfirmedNoComments = false;
            CommentsErrorMessage = "There was an error loading comments. Please try again.";
            AreCommentsVisible = false;
            _webCommentRoots = null;
            WebCommentNodes = [];
            ShowCommentsEnd = false;
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

        var story = SelectedStory;

        if (_commentCache.TryGetValue(storyId, out var cached) && cached != null)
        {
            _webCommentRoots = cached;

            // Comments came from cache, so no page fetch happened this time — if
            // the descendant count still looks stale, refresh it in the background
            // without blocking comment rendering on a network round-trip.
            if (story.Descendants is null or 0)
            {
                _ = RefreshAccurateCommentCountAsync(story, storyId, cancellationToken);
            }
        }
        else
        {
            var (webComments, accurateCount) = await GetCommentsWithEmptyRetryAsync(story, storyId, cancellationToken);

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
            // (notably Ask HN posts). The accurate count already came back with
            // the comments in the same page fetch above, so no extra round-trip
            // is needed here.
            if (_webCommentRoots.Count > 0 && story.Descendants is null or 0 && accurateCount > 0)
            {
                story.Descendants = accurateCount;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        await LoadCommentsBatchedAsync(_webCommentRoots ?? [], cancellationToken);
    }

    /// <summary>
    /// Fires-and-forgets a comment-count refresh for the comments-cache-hit path,
    /// where we have comments but skipped fetching the page (so the descendant
    /// count may still be stale). Never awaited by the caller — a badge number
    /// updating a moment late isn't worth blocking comment rendering on.
    /// </summary>
    private async Task RefreshAccurateCommentCountAsync(Story story, int storyId, CancellationToken cancellationToken)
    {
        try
        {
            var accurateCount = await _webClient.GetAccurateCommentCountAsync(storyId, cancellationToken);
            if (!cancellationToken.IsCancellationRequested && accurateCount > 0)
            {
                story.Descendants = accurateCount;
            }
        }
        catch (OperationCanceledException)
        {
            // The story selection moved on before this finished — fine to drop.
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("PageViewModel", "background comment count refresh failed", ex);
        }
    }

    private async Task<(List<WebComment> Comments, int AccurateCount)> GetCommentsWithEmptyRetryAsync(Story story, int storyId, CancellationToken cancellationToken)
    {
        var (webComments, accurateCount) = await _webClient.FetchCommentsPageAsync(storyId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (webComments.Count > 0)
        {
            // The same fetch already tells us both the comments and the row
            // count — no need for a separate confirmatory request.
            return (webComments, accurateCount);
        }

        // The HTML parser returned no comments. This could be genuinely
        // comment-free, or the page could have been momentarily stale (HN's
        // page occasionally lags right after a story is created) — re-check
        // with a fresh request before deciding, rather than trusting the one
        // possibly-stale snapshot we already have.
        var freshCount = await _webClient.GetAccurateCommentCountAsync(storyId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (freshCount == 0)
        {
            HasConfirmedNoComments = true;
            return (webComments, freshCount);
        }

        story.Descendants = freshCount;

        // The API/HTML both report comments but the parser found none — try
        // once more against a fresh fetch, then give up.
        var (retryComments, retryCount) = await _webClient.FetchCommentsPageAsync(storyId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (retryComments.Count > 0)
        {
            return (retryComments, retryCount);
        }

        throw new InvalidOperationException("Hacker News reported comments, but no comments could be parsed.");
    }

    private async Task LoadCommentsBatchedAsync(List<WebCommentNode> roots, CancellationToken cancellationToken)
    {
        const int batchSize = 15;

        if (roots == null || roots.Count == 0)
        {
            WebCommentNodes = [];
            ShowCommentsEnd = false;
            OnPropertyChanged(nameof(ShowWebComments));
            OnPropertyChanged(nameof(ShowNoCommentsMessage));
            return;
        }

        WebCommentNodes = [];
        ShowCommentsEnd = false;
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

        ShowCommentsEnd = true;
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
               (!string.IsNullOrEmpty(story.DisplayDomain) && story.DisplayDomain.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
