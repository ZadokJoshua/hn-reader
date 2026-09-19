using HNReader.Core.Interfaces;
using HNReader.Core.Models;
using HNReader.Core.Services;
using HNReader.Core.Viewmodels;
using System.Net;

namespace HNReader.Core.Tests.Viewmodels;

/// <summary>
/// Covers how the filtered (list-bound) collection is updated when more stories
/// load. The distinction matters for more than tidiness: rebuilding that
/// collection made the list drop and re-acquire its selection, and re-setting
/// the selection re-triggered the list's AutoScrollToSelectedItem, which threw
/// the viewport back up to the selected story right after the user had scrolled
/// to the bottom to load more. Appending must therefore leave the selection
/// completely untouched.
/// </summary>
public class LoadMoreSelectionTests
{
    [Fact]
    public void AppendToSearchFilter_LeavesSelectionUntouched()
    {
        var viewModel = CreateViewModel();
        var (first, second) = SeedTwoStories(viewModel);

        viewModel.SelectedStory = first;

        viewModel.AppendStories([second]);

        // Referential identity, not just equality: it is the *reassignment* of
        // SelectedStory that re-triggers the scroll, so nothing may re-set it.
        Assert.Same(first, viewModel.SelectedStory);
    }

    [Fact]
    public void AppendToSearchFilter_KeepsExistingItemsThenAppendsNewOnes()
    {
        var viewModel = CreateViewModel();
        var (first, second) = SeedTwoStories(viewModel);

        var third = new Story { Id = 3, Title = "Third story" };
        viewModel.Stories.Add(third);

        viewModel.AppendStories([third]);

        Assert.Equal([first, second, third], viewModel.FilteredStories);
    }

    [Fact]
    public void AppendToSearchFilter_ExcludesNewStoriesNotMatchingActiveSearch()
    {
        var viewModel = CreateViewModel();
        var (first, _) = SeedTwoStories(viewModel);

        // Setting SearchText rebuilds the filter, leaving only "First story".
        viewModel.SearchText = "First";
        Assert.Equal([first], viewModel.FilteredStories);

        var matching = new Story { Id = 3, Title = "First story, continued" };
        var nonMatching = new Story { Id = 4, Title = "Unrelated story" };
        viewModel.Stories.Add(matching);
        viewModel.Stories.Add(nonMatching);

        viewModel.AppendStories([matching, nonMatching]);

        Assert.Equal([first, matching], viewModel.FilteredStories);
    }

    [Fact]
    public void ApplySearchFilter_StillPreservesSelectionOnRebuild()
    {
        var viewModel = CreateViewModel();
        var (first, _) = SeedTwoStories(viewModel);

        viewModel.SelectedStory = first;

        // The rebuild path is still used by full reloads and search changes, and
        // its own selection-preservation must keep working.
        viewModel.RebuildFilter();

        Assert.Same(first, viewModel.SelectedStory);
    }

    /// <summary>
    /// Seeds two stories through the rebuild path, so the view model starts in
    /// the same state a first page load would leave it in.
    /// </summary>
    private static (Story First, Story Second) SeedTwoStories(TestPageViewModel viewModel)
    {
        var first = new Story { Id = 1, Title = "First story" };
        var second = new Story { Id = 2, Title = "Second story" };

        viewModel.Stories.Add(first);
        viewModel.Stories.Add(second);
        viewModel.RebuildFilter();

        return (first, second);
    }

    private static TestPageViewModel CreateViewModel()
    {
        return new TestPageViewModel(
            CreateHnClient(),
            new Lazy<IFavoritesService>(() => new TestFavoritesService()),
            CreateWebClient());
    }

    private static HNClient CreateHnClient()
    {
        return new HNClient(new HttpClient(new EmptyResponseHandler())
        {
            BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/")
        });
    }

    private static HNWebClient CreateWebClient()
    {
        return new HNWebClient(new HttpClient(new EmptyResponseHandler())
        {
            BaseAddress = new Uri("https://news.ycombinator.com/")
        });
    }

    /// <summary>
    /// Exposes the two protected filter paths. Testing them directly rather than
    /// through LoadMoreStoriesAsync avoids having to fake the whole Firebase
    /// paging protocol for what is a pure collection-update concern.
    /// </summary>
    private sealed class TestPageViewModel : TopPageViewModel
    {
        public TestPageViewModel(HNClient client, Lazy<IFavoritesService> favoritesService, HNWebClient webClient)
            : base(client, favoritesService, webClient)
        {
        }

        public void AppendStories(IEnumerable<Story> stories) => AppendToSearchFilter(stories);

        public void RebuildFilter() => ApplySearchFilter();
    }

    private sealed class EmptyResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            });
        }
    }

    private sealed class TestFavoritesService : IFavoritesService
    {
        public event EventHandler? FavoritesChanged
        {
            add { }
            remove { }
        }

        public Task AddOrUpdateAsync(Story story) => Task.CompletedTask;

        public Task RemoveAsync(int storyId) => Task.CompletedTask;

        public Task<bool> ExistsAsync(int storyId) => Task.FromResult(false);

        public Task<List<Story>> GetAllAsync() => Task.FromResult(new List<Story>());

        public Task<List<int>> GetAllIdsAsync() => Task.FromResult(new List<int>());

        public Task<int> ExportToFileAsync(string filePath) => Task.FromResult(0);

        public Task<int> ImportFromFileAsync(string filePath) => Task.FromResult(0);
    }
}
