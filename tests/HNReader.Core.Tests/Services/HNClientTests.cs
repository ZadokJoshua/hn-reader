using HNReader.Core.Enums;
using HNReader.Core.Services;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace HNReader.Core.Tests.Services;

public class HNClientTests
{
    [Fact]
    public async Task GetStoriesPageAsync_SkipsDeadItemsAndAdvancesCursorAcrossThem()
    {
        var handler = new RoutingHandler(
            [1, 2, 3, 4, 5],
            new Dictionary<int, string>
            {
                [1] = StoryJson(1, "First"),
                [2] = "null",
                [3] = StoryJson(3, "Third"),
                [4] = StoryJson(4, "Fourth"),
                [5] = StoryJson(5, "Fifth")
            });
        var client = CreateClient(handler);

        var (firstPage, nextOffset) = await client.GetStoriesPageAsync(StoryType.Top, limit: 2);

        Assert.Equal(new[] { 1, 3 }, firstPage.Select(story => story.Id));
        Assert.Equal(3, nextOffset);

        var (secondPage, secondNextOffset) = await client.GetStoriesPageAsync(
            StoryType.Top, limit: 2, offset: nextOffset);

        Assert.Equal(new[] { 4, 5 }, secondPage.Select(story => story.Id));
        Assert.Equal(5, secondNextOffset);
        Assert.Equal(1, handler.FeedRequestCount);
    }

    [Fact]
    public async Task ClearCache_CausesNextPageToReloadFeedIds()
    {
        var handler = new RoutingHandler(
            [10, 11],
            new Dictionary<int, string>
            {
                [10] = StoryJson(10, "Ten"),
                [11] = StoryJson(11, "Eleven")
            });
        var client = CreateClient(handler);

        var (firstPage, nextOffset) = await client.GetStoriesPageAsync(StoryType.Top, limit: 1);
        Assert.Equal(10, Assert.Single(firstPage).Id);
        Assert.Equal(1, handler.FeedRequestCount);

        client.ClearCache();
        var (secondPage, _) = await client.GetStoriesPageAsync(StoryType.Top, limit: 1, offset: nextOffset);

        Assert.Equal(11, Assert.Single(secondPage).Id);
        Assert.Equal(2, handler.FeedRequestCount);
    }

    private static HNClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/")
        });

    private static string StoryJson(int id, string title) => JsonSerializer.Serialize(new
    {
        id,
        type = "story",
        title,
        by = "author",
        time = 1_700_000_000,
        score = 10,
        kids = Array.Empty<int>(),
        descendants = 0
    });

    private sealed class RoutingHandler(List<int> feedIds, Dictionary<int, string> items) : HttpMessageHandler
    {
        private readonly ConcurrentQueue<string> _paths = new();

        public int FeedRequestCount => _paths.Count(path => path.EndsWith("stories.json", StringComparison.Ordinal));

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            _paths.Enqueue(path);

            var body = path.EndsWith("stories.json", StringComparison.Ordinal)
                ? JsonSerializer.Serialize(feedIds)
                : int.TryParse(Path.GetFileNameWithoutExtension(path), out var id) && items.TryGetValue(id, out var json)
                    ? json
                    : throw new InvalidOperationException($"Unexpected request path: {path}");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
        }
    }
}

