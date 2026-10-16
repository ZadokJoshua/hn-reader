using System.Net;
using HNReader.Core.Interfaces;
using HNReader.Core.Models;
using HNReader.Core.Services;
using HNReader.Core.Viewmodels;

namespace HNReader.Core.Tests.Viewmodels;

public class PageLoadErrorTests
{
    private sealed class FailingHandler(Func<Exception> make) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw make();
    }

    private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("") });
    }

    private sealed class NoFavourites : IFavoritesService
    {
        public event EventHandler? FavoritesChanged { add { } remove { } }
        public Task AddOrUpdateAsync(Story story) => Task.CompletedTask;
        public Task RemoveAsync(int storyId) => Task.CompletedTask;
        public Task<bool> ExistsAsync(int storyId) => Task.FromResult(false);
        public Task<List<Story>> GetAllAsync() => Task.FromResult(new List<Story>());
        public Task<List<int>> GetAllIdsAsync() => Task.FromResult(new List<int>());
        public Task<int> ExportToFileAsync(string filePath) => Task.FromResult(0);
        public Task<int> ImportFromFileAsync(string filePath) => Task.FromResult(0);
        public Task<FavoritesImportResult> ImportAsync(string filePath) => Task.FromResult(default(FavoritesImportResult));
    }

    private static TopPageViewModel Create(HttpMessageHandler hn, HttpMessageHandler web)
    {
        var client = new HNClient(new HttpClient(hn) { BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/") });
        var webClient = new HNWebClient(new HttpClient(web) { BaseAddress = new Uri("https://news.ycombinator.com/") });
        return new TopPageViewModel(client, new Lazy<IFavoritesService>(() => new NoFavourites()), webClient);
    }

    [Fact]
    public async Task InitialLoad_WhenOffline_ShowsOfflineMessage()
    {
        var vm = Create(new FailingHandler(() => new HttpRequestException("No such host is known")),
                        new StatusHandler(HttpStatusCode.OK));

        await vm.PopulateListAsync();

        Assert.True(vm.HasError);
        Assert.True(vm.IsOfflineError);
        Assert.Equal("You're offline", vm.ErrorTitle);
        Assert.Contains("offline", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No such host", vm.ErrorMessage);
    }

    [Fact]
    public async Task InitialLoad_WhenServerBusy_ShowsBusyMessageNotOffline()
    {
        var vm = Create(new StatusHandler(HttpStatusCode.TooManyRequests), new StatusHandler(HttpStatusCode.OK));

        await vm.PopulateListAsync();

        Assert.True(vm.HasError);
        Assert.False(vm.IsOfflineError);
        Assert.Contains("busy", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InitialLoad_WhenTimedOut_ShowsTimeoutMessage()
    {
        var vm = Create(new FailingHandler(() => new TaskCanceledException("timeout", new TimeoutException())),
                        new StatusHandler(HttpStatusCode.OK));

        await vm.PopulateListAsync();

        Assert.True(vm.HasError);
        Assert.Contains("too long", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Comments_WhenOffline_ShowFriendlyErrorNotSilence()
    {
        var vm = Create(new StatusHandler(HttpStatusCode.OK),
                        new FailingHandler(() => new HttpRequestException("connection refused")));
        vm.SelectedStory = new Story { Id = 42, Title = "t", Descendants = 3 };

        await vm.ToggleCommentsCommand.ExecuteAsync(null);

        Assert.True(vm.HasCommentsError);
        Assert.Contains("offline", vm.CommentsErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(vm.ShowNoCommentsMessage);
    }

    [Fact]
    public async Task Comments_WhenHttpClientTimesOut_ShowErrorNotSilentCancel()
    {
        var vm = Create(new StatusHandler(HttpStatusCode.OK),
                        new FailingHandler(() => new TaskCanceledException("timeout", new TimeoutException())));
        vm.SelectedStory = new Story { Id = 43, Title = "t", Descendants = 3 };

        await vm.ToggleCommentsCommand.ExecuteAsync(null);

        Assert.True(vm.HasCommentsError);
    }
}
