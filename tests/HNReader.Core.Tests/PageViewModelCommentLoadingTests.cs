using HNReader.Core.Interfaces;
using HNReader.Core.Models;
using HNReader.Core.Services;
using HNReader.Core.Viewmodels;
using System.Net;

namespace HNReader.Core.Tests;

public class PageViewModelCommentLoadingTests
{
    [Fact]
    public async Task ToggleCommentsAsync_WhenLargeThreadFirstReturnsEmpty_RetriesAndShowsComments()
    {
        var webHandler = new SequenceResponseHandler();
        webHandler.Enqueue(HttpStatusCode.OK, EmptyItemHtml);
        webHandler.Enqueue(HttpStatusCode.OK, CommentItemHtml);
        var vm = CreateViewModel(webHandler);
        vm.SelectedStory = new Story { Id = 10001, Title = "Large thread", Descendants = 350 };

        await vm.ToggleCommentsCommand.ExecuteAsync(null);

        Assert.False(vm.HasCommentsError);
        Assert.True(vm.AreCommentsVisible);
        Assert.False(vm.ShowNoCommentsMessage);
        Assert.Single(vm.WebCommentNodes);
        Assert.Equal(2, webHandler.RequestCount);
    }

    [Fact]
    public async Task ToggleCommentsAsync_WhenReportedCommentsStayEmpty_ShowsErrorAndDoesNotCacheEmptyResult()
    {
        var webHandler = new SequenceResponseHandler();
        webHandler.Enqueue(HttpStatusCode.OK, EmptyItemHtml);
        webHandler.Enqueue(HttpStatusCode.OK, EmptyItemHtml);
        var vm = CreateViewModel(webHandler);
        vm.SelectedStory = new Story { Id = 10002, Title = "Missing comments", Descendants = 350 };

        await vm.ToggleCommentsCommand.ExecuteAsync(null);

        Assert.True(vm.HasCommentsError);
        Assert.False(vm.AreCommentsVisible);
        Assert.False(vm.ShowNoCommentsMessage);
        Assert.Empty(vm.WebCommentNodes);
        Assert.Equal(2, webHandler.RequestCount);

        webHandler.Enqueue(HttpStatusCode.OK, CommentItemHtml);

        await vm.ToggleCommentsCommand.ExecuteAsync(null);

        Assert.False(vm.HasCommentsError);
        Assert.True(vm.AreCommentsVisible);
        Assert.Single(vm.WebCommentNodes);
        Assert.Equal(3, webHandler.RequestCount);
    }

    [Fact]
    public async Task ToggleCommentsAsync_WhenStoryHasConfirmedZeroComments_ShowsNoCommentsMessage()
    {
        var webHandler = new SequenceResponseHandler();
        webHandler.Enqueue(HttpStatusCode.OK, EmptyItemHtml);
        webHandler.Enqueue(HttpStatusCode.OK, EmptyItemHtml);
        var vm = CreateViewModel(webHandler);
        vm.SelectedStory = new Story { Id = 10003, Title = "Quiet story", Descendants = 0 };

        await vm.ToggleCommentsCommand.ExecuteAsync(null);

        Assert.False(vm.HasCommentsError);
        Assert.True(vm.AreCommentsVisible);
        Assert.True(vm.ShowNoCommentsMessage);
        Assert.Empty(vm.WebCommentNodes);
        Assert.Equal(2, webHandler.RequestCount);
    }

    private static TopPageViewModel CreateViewModel(HttpMessageHandler webHandler)
    {
        var hnClient = new HNClient(new HttpClient(new SequenceResponseHandler())
        {
            BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/")
        });

        var webClient = new HNWebClient(new HttpClient(webHandler)
        {
            BaseAddress = new Uri("https://news.ycombinator.com/")
        });

        return new TopPageViewModel(
            hnClient,
            new Lazy<IFavoritesService>(() => new TestFavoritesService()),
            webClient);
    }

    private const string EmptyItemHtml = """
        <html>
            <body>
                <table>
                    <tr class="athing" id="100"><td>No comments.</td></tr>
                </table>
            </body>
        </html>
        """;

    private const string CommentItemHtml = """
        <html>
            <body>
                <table>
                    <tr class="athing comtr" id="20001">
                        <td class="ind" indent="0"></td>
                        <td>
                            <div class="comment">
                                <span class="comhead">
                                    <a class="hnuser">alice</a>
                                    <span class="age" title="2026-06-23T12:00:00 1782216000"></span>
                                </span>
                                <div class="commtext">First comment</div>
                            </div>
                        </td>
                    </tr>
                </table>
            </body>
        </html>
        """;

    private sealed class SequenceResponseHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public int RequestCount { get; private set; }

        public void Enqueue(HttpStatusCode statusCode, string body)
        {
            _responses.Enqueue(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body)
            });
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;

            if (_responses.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]")
                });
            }

            return Task.FromResult(_responses.Dequeue());
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
