using System.Net;
using System.Text;
using HNReader.Core.Models;
using HNReader.Core.Services;

namespace HNReader.Core.Tests.Services;

public class DigestClientTests
{
    /// <summary>
    /// The exact payload shape HNReader.Server produces: ASP.NET's default
    /// camelCase over the OperationResponse envelope, with a null url (self-post)
    /// on the second item and a null imageUrl on the first.
    /// </summary>
    private const string DigestJson = """
    {
      "success": true,
      "error": null,
      "data": {
        "generatedAtUtc": "2026-09-02T04:30:00Z",
        "categories": [
          {
            "name": "Security & Privacy",
            "summary": "Two stories about device security today.",
            "items": [
              {
                "storyId": 49526131,
                "title": "GrapheneOS may skip Pixel 11",
                "url": "https://example.com/grapheneos",
                "hackerNewsUrl": "https://news.ycombinator.com/item?id=49526131",
                "summary": "GrapheneOS may abandon support for the Pixel 11.",
                "author": "someone",
                "commentNote": "Commenters expressed concern over proprietary security.",
                "imageUrl": null
              },
              {
                "storyId": 49521623,
                "title": "Ask HN: How do you audit dependencies?",
                "url": null,
                "hackerNewsUrl": "https://news.ycombinator.com/item?id=49521623",
                "summary": "A discussion about dependency auditing practices.",
                "author": "asker",
                "commentNote": null,
                "imageUrl": "https://example.com/preview.png"
              }
            ]
          }
        ]
      }
    }
    """;

    private static DigestClient CreateClient(MockHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5282/") });

    /// <summary>
    /// The regression test for this feature's sharpest edge. The server sends
    /// camelCase; System.Text.Json matches property names case-sensitively by
    /// default, and an unmatched positional-record constructor parameter is
    /// filled with a default instead of throwing. Deserialized with the wrong
    /// options this returns a DigestDto with a default timestamp and a null
    /// category list, and raises nothing at all — so this asserts the fields are
    /// actually populated rather than merely that no exception escaped.
    /// </summary>
    [Fact]
    public async Task GetDigestAsync_BindsCamelCaseServerPayload()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, DigestJson);
        var client = CreateClient(handler);

        var result = await client.GetDigestAsync();

        Assert.Equal(DigestStatus.Ok, result.Status);
        Assert.NotNull(result.Digest);
        Assert.Equal(new DateTime(2026, 9, 2, 4, 30, 0, DateTimeKind.Utc), result.Digest!.GeneratedAtUtc.ToUniversalTime());

        var category = Assert.Single(result.Digest.Categories);
        Assert.Equal("Security & Privacy", category.Name);
        Assert.Equal("Two stories about device security today.", category.Summary);

        var items = category.Items.ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("GrapheneOS may skip Pixel 11", items[0].Title);
        Assert.Equal("https://news.ycombinator.com/item?id=49526131", items[0].HackerNewsUrl);
        Assert.Equal(49526131, items[0].StoryId);
        Assert.Equal("someone", items[0].Author);
    }

    [Fact]
    public async Task GetDigestAsync_SelfPost_FallsBackToHackerNewsUrl()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, DigestJson);
        var client = CreateClient(handler);

        var result = await client.GetDigestAsync();
        var dto = result.Digest!.Categories.Single().Items.ToList()[1];
        var item = DigestItem.From(dto);

        Assert.Null(item.Url);
        Assert.False(item.HasArticle);
        Assert.Equal("https://news.ycombinator.com/item?id=49521623", item.PrimaryUrl);
        Assert.Null(item.RootDomain);
    }

    [Fact]
    public async Task GetDigestAsync_ArticleItem_ExposesRootDomainAndNote()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, DigestJson);
        var client = CreateClient(handler);

        var result = await client.GetDigestAsync();
        var item = DigestItem.From(result.Digest!.Categories.Single().Items.First());

        Assert.True(item.HasArticle);
        Assert.Equal("example.com", item.RootDomain);
        Assert.True(item.HasNote);
        Assert.False(item.HasImage);
    }

    /// <summary>
    /// A 404 here is documented, expected behaviour before the first nightly run
    /// — so it must be an outcome, not an exception.
    /// </summary>
    [Fact]
    public async Task GetDigestAsync_NotFound_ReportsNotGeneratedWithServerMessage()
    {
        const string body = """{"success":false,"error":"No digest has been generated yet.","data":null}""";
        var handler = new MockHttpMessageHandler(HttpStatusCode.NotFound, body);
        var client = CreateClient(handler);

        var result = await client.GetDigestAsync();

        Assert.Equal(DigestStatus.NotGenerated, result.Status);
        Assert.Equal("No digest has been generated yet.", result.ServerMessage);
        Assert.Null(result.Digest);
    }

    [Fact]
    public async Task GetDigestAsync_TooManyRequests_ReportsRateLimited()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.TooManyRequests, string.Empty);
        var client = CreateClient(handler);

        var result = await client.GetDigestAsync();

        Assert.Equal(DigestStatus.RateLimited, result.Status);
    }

    [Fact]
    public async Task GetDigestAsync_ServerError_Throws()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError, "boom");
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetDigestAsync());
    }

    /// <summary>
    /// A 200 whose envelope reports failure must not be turned into an
    /// empty-but-successful digest.
    /// </summary>
    [Fact]
    public async Task GetDigestAsync_SuccessfulStatusButFailedEnvelope_ReportsNotGenerated()
    {
        const string body = """{"success":false,"error":"Something went wrong.","data":null}""";
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, body);
        var client = CreateClient(handler);

        var result = await client.GetDigestAsync();

        Assert.Equal(DigestStatus.NotGenerated, result.Status);
        Assert.Equal("Something went wrong.", result.ServerMessage);
    }

    /// <summary>
    /// Category names contain spaces and ampersands. Left unescaped, the '&'
    /// starts a new query parameter and silently truncates the filter.
    /// </summary>
    [Fact]
    public async Task GetDigestAsync_EscapesCategoryNames()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, DigestJson);
        var client = CreateClient(handler);

        await client.GetDigestAsync(["Startups, Funding & Business", "Science & Research"]);

        var query = handler.LastRequestUri!.Query;
        Assert.DoesNotContain("Funding & Business", query);
        Assert.Contains("Startups%2C%20Funding%20%26%20Business", query);
        Assert.Contains("Science%20%26%20Research", query);
    }

    [Fact]
    public async Task GetDigestAsync_WithoutCategories_OmitsQuery()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, DigestJson);
        var client = CreateClient(handler);

        await client.GetDigestAsync();

        Assert.Equal("/digest", handler.LastRequestUri!.AbsolutePath);
        Assert.Empty(handler.LastRequestUri.Query);
    }

    [Fact]
    public async Task GetCategoriesAsync_BindsCamelCasePayload()
    {
        const string body = """
        {"success":true,"error":null,"data":[{"name":"Programming & Software Engineering","isEnabled":true},
                                             {"name":"Science & Research","isEnabled":true}]}
        """;
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, body);
        var client = CreateClient(handler);

        var names = await client.GetCategoriesAsync();

        Assert.Equal(["Programming & Software Engineering", "Science & Research"], names);
    }

    /// <summary>
    /// The taxonomy only affects ordering and labelling, so losing it must not
    /// sink a digest that fetched perfectly well.
    /// </summary>
    [Fact]
    public async Task GetCategoriesAsync_ServerError_ReturnsEmptyWithoutThrowing()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError, "boom");
        var client = CreateClient(handler);

        var names = await client.GetCategoriesAsync();

        Assert.Empty(names);
    }

    /// <summary>
    /// Returns a canned status and body, and records what was asked for. Extends
    /// the handler pattern already used by <see cref="HNClientTests"/>.
    /// </summary>
    internal sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;

        public MockHttpMessageHandler(HttpStatusCode statusCode, string body)
        {
            _statusCode = statusCode;
            _body = body;
        }

        public Uri? LastRequestUri { get; private set; }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            RequestCount++;

            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }
}
