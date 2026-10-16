using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using HNReader.Core.Models;
using HNReader.Core.Services;

namespace HNReader.Core.Tests.Services;

/// <summary>
/// Verifies the accurate comment count fetched from the HTML page is used to
/// correct the Firebase API's sometimes-wrong descendant count. The HN Firebase
/// API regularly returns 0 descendants for stories that actually have hundreds
/// of comments (notably Ask HN posts); counting the &lt;tr class="comtr"&gt; rows in
/// the live page is authoritative.
/// </summary>
public class HNWebClientAccurateCountTests
{
    [Fact]
    public async Task GetAccurateCommentCountAsync_WithSampleHtml_ReturnsRowCount()
    {
        const string html = @"
            <html><body>
                <table>
                    <tr class='athing comtr'><td>comment 1</td></tr>
                    <tr class='athing comtr'><td>comment 2</td></tr>
                    <tr class='athing comtr'><td>comment 3</td></tr>
                </table>
            </body></html>";

        var handler = new SingleResponseHandler(html);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://news.ycombinator.com/") };
        var client = new HNWebClient(httpClient);

        var count = await client.GetAccurateCommentCountAsync(123);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task GetAccurateCommentCountAsync_WithNoComments_ReturnsZero()
    {
        const string html = "<html><body><p>No comments.</p></body></html>";

        var handler = new SingleResponseHandler(html);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://news.ycombinator.com/") };
        var client = new HNWebClient(httpClient);

        var count = await client.GetAccurateCommentCountAsync(123);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetAccurateCommentCountAsync_WhenRequestFails_ReturnsZero()
    {
        var handler = new FailHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://news.ycombinator.com/") };
        var client = new HNWebClient(httpClient);

        var count = await client.GetAccurateCommentCountAsync(123);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetCommentsFromWebAsync_WhenRequestFails_ThrowsHttpRequestException()
    {
        var handler = new FailHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://news.ycombinator.com/") };
        var client = new HNWebClient(httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetCommentsFromWebAsync(123));
    }

    [Fact]
    public async Task FetchCommentsPageAsync_ParsesDepthFiltersDeletedRowsAndUsesOneRequest()
    {
        const string html = """
            <html><body><table>
                <tr class="athing comtr" id="20001">
                    <td class="ind" indent="0"></td>
                    <td><a class="hnuser">alice</a>
                        <span class="age" title="2026-06-23T12:00:00 1782216000"></span>
                        <div class="commtext">Root comment <a href="reply?id=1">reply</a><span class="par">parent</span></div>
                    </td>
                </tr>
                <tr class="athing comtr" id="20002">
                    <td class="ind" indent="2"></td>
                    <td><a class="hnuser">bob</a>
                        <span class="age" title="2026-06-23T12:01:00 1782216060"></span>
                        <div class="commtext">Nested comment</div>
                    </td>
                </tr>
                <tr class="athing comtr" id="20003">
                    <td class="ind" indent="0"></td>
                    <td><a class="hnuser">deleted-author</a>
                        <div class="commtext">[deleted]</div>
                    </td>
                </tr>
            </table></body></html>
            """;

        var handler = new RecordingResponseHandler(html);
        var client = new HNWebClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://news.ycombinator.com/")
        });

        var (comments, accurateCount) = await client.FetchCommentsPageAsync(123);

        Assert.Equal(3, accurateCount);
        Assert.Equal(2, comments.Count);
        Assert.Equal(new[] { 0, 2 }, comments.Select(comment => comment.Depth));
        Assert.Equal(new[] { "alice", "bob" }, comments.Select(comment => comment.By));
        Assert.Contains("Root comment", comments[0].Text);
        Assert.DoesNotContain("reply", comments[0].Text);
        Assert.DoesNotContain("parent", comments[0].Text);
        Assert.Equal(1782216000, comments[0].Time);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal("/item", handler.RequestUri!.AbsolutePath);
        Assert.Equal("id=123", handler.RequestUri.Query.TrimStart('?'));
    }

    [Fact]
    public async Task ConcurrentRequestsForSameStoryAcrossClients_ShareOneHtmlFetch()
    {
        const string html = "<html><body>No comments.</body></html>";
        var handler = new BlockingResponseHandler(html);
        var coordinator = new HNWebRequestCoordinator(minimumRequestInterval: TimeSpan.Zero);
        var firstClient = CreateClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://news.ycombinator.com/")
        }, coordinator);
        var secondClient = CreateClient(new HttpClient(new RecordingResponseHandler(html))
        {
            BaseAddress = new Uri("https://news.ycombinator.com/")
        }, coordinator);

        var firstRequest = firstClient.FetchCommentsPageAsync(123);
        await handler.RequestStarted;
        var secondRequest = secondClient.GetAccurateCommentCountAsync(123);

        Assert.Equal(1, handler.RequestCount);
        handler.Release();

        var (comments, count) = await firstRequest;
        Assert.Empty(comments);
        Assert.Equal(0, count);
        Assert.Equal(0, await secondRequest);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task RequestsForDifferentStories_RespectConfiguredMinimumSpacing()
    {
        const string html = "<html><body>No comments.</body></html>";
        var handler = new RecordingResponseHandler(html);
        var coordinator = new HNWebRequestCoordinator(
            minimumRequestInterval: TimeSpan.FromMilliseconds(80));
        var client = CreateClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://news.ycombinator.com/")
        }, coordinator);

        await client.GetAccurateCommentCountAsync(123);
        var stopwatch = Stopwatch.StartNew();

        await client.GetAccurateCommentCountAsync(456);

        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(50),
            $"The next HTML request started too early: {stopwatch.Elapsed}");
        Assert.Equal(2, handler.RequestCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task TransientServerResponse_HonorsRetryAfterBeforeNextHtmlRequest(HttpStatusCode status)
    {
        const string html = "<html><body>No comments.</body></html>";
        var handler = new RetryAfterResponseHandler(status, html, TimeSpan.FromMilliseconds(150));
        var coordinator = new HNWebRequestCoordinator(minimumRequestInterval: TimeSpan.Zero);
        var client = CreateClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://news.ycombinator.com/")
        }, coordinator);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetCommentsFromWebAsync(123));

        var stopwatch = Stopwatch.StartNew();
        Assert.Equal(0, await client.GetAccurateCommentCountAsync(456));

        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(100),
            $"The retry started too early after Retry-After: {stopwatch.Elapsed}");
        Assert.Equal(2, handler.RequestCount);
    }

    private class SingleResponseHandler : HttpMessageHandler
    {
        private readonly string _body;
        public SingleResponseHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(_body)
            });
        }
    }

    private class FailHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));
        }
    }

    private sealed class RecordingResponseHandler(string body) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
        }
    }

    private static HNWebClient CreateClient(HttpClient httpClient, HNWebRequestCoordinator coordinator) =>
        new(httpClient, requestCoordinator: coordinator);

    private sealed class BlockingResponseHandler(string body) : HttpMessageHandler
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _requestStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int RequestCount { get; private set; }
        public Task RequestStarted => _requestStarted.Task;
        public void Release() => _release.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            _requestStarted.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            };
        }
    }

    private sealed class RetryAfterResponseHandler(
        HttpStatusCode firstStatus,
        string body,
        TimeSpan retryAfter) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            var response = RequestCount == 1
                ? new HttpResponseMessage(firstStatus)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };

            if (RequestCount == 1)
                response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter);

            return Task.FromResult(response);
        }
    }
}
