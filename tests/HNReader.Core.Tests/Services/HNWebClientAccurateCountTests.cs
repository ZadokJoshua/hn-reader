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
}
