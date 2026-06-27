using HNReader.Core.Interfaces;
using HNReader.Core.Models;
using HNReader.Core.Services;
using HNReader.Core.Viewmodels;
using System.Net;

namespace HNReader.Core.Tests.Viewmodels;

public class LoadMoreFooterVisibilityTests
{
    [Fact]
    public void ShowLoadMoreFooter_ForNonEmptyFavorites_IsFalse()
    {
        var viewModel = CreateFavoritesViewModel();

        viewModel.Stories.Add(new Story { Id = 1, Title = "Saved story" });

        Assert.False(viewModel.ShowLoadMoreFooter);
    }

    [Fact]
    public void ShowLoadMoreFooter_ForNonEmptyPagedFeed_IsTrue()
    {
        var viewModel = CreateTopViewModel();

        viewModel.Stories.Add(new Story { Id = 1, Title = "Top story" });

        Assert.True(viewModel.ShowLoadMoreFooter);
    }

    private static FavouritesPageViewModel CreateFavoritesViewModel()
    {
        return new FavouritesPageViewModel(
            CreateHnClient(),
            new Lazy<IFavoritesService>(() => new TestFavoritesService()),
            CreateWebClient());
    }

    private static TopPageViewModel CreateTopViewModel()
    {
        return new TopPageViewModel(
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
