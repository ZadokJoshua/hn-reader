using System.Net;
using System.Text;
using HNReader.Core.Services;

namespace HNReader.Core.Tests.Services;

public class FaviconServiceTests
{
    // Smallest bytes that pass the magic-byte sniff for each format we accept.
    private static byte[] Png() => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };
    private static byte[] Ico() => new byte[] { 0x00, 0x00, 0x01, 0x00, 1, 0, 16, 16, 0, 0, 0, 0 };

    private static FaviconService CreateService(QueuedHandler handler) =>
        // No disk directory: these tests exercise the chain and the memory tiers only.
        new(new HttpClient(handler), diskCacheDirectory: null, logger: null);

    [Fact]
    public async Task FirstRungHit_ReturnsBytes_AndStopsThere()
    {
        var handler = new QueuedHandler();
        handler.Enqueue(HttpStatusCode.OK, Png(), "image/png");

        var bytes = await CreateService(handler).GetFaviconAsync("example.com");

        Assert.NotNull(bytes);
        Assert.Equal(Png(), bytes);
        Assert.Equal(1, handler.RequestCount);
        Assert.Contains("icons.duckduckgo.com", handler.RequestedUris[0]);
    }

    [Fact]
    public async Task FallsThroughToDirectFavicon_WhenFirstRungMisses()
    {
        var handler = new QueuedHandler();
        handler.Enqueue(HttpStatusCode.NotFound, Array.Empty<byte>(), "text/plain");
        handler.Enqueue(HttpStatusCode.OK, Ico(), "image/x-icon");

        var bytes = await CreateService(handler).GetFaviconAsync("example.com");

        Assert.Equal(Ico(), bytes);
        Assert.Equal(2, handler.RequestCount);
        Assert.Contains("icons.duckduckgo.com", handler.RequestedUris[0]);
        Assert.Contains("https://example.com/favicon.ico", handler.RequestedUris[1]);
    }

    [Fact]
    public async Task GoogleIsTriedLast()
    {
        // Google answers 200 with a generic globe for domains it has never seen, so trying
        // it earlier would end the chain before the site's own icon is ever requested.
        var handler = new QueuedHandler();
        handler.Enqueue(HttpStatusCode.NotFound, Array.Empty<byte>(), "text/plain");
        handler.Enqueue(HttpStatusCode.NotFound, Array.Empty<byte>(), "text/plain");
        handler.Enqueue(HttpStatusCode.OK, Png(), "image/png");

        var bytes = await CreateService(handler).GetFaviconAsync("example.com");

        Assert.NotNull(bytes);
        Assert.Equal(3, handler.RequestCount);
        Assert.Contains("google.com/s2/favicons", handler.RequestedUris[2]);
    }

    [Fact]
    public async Task RejectsHtmlBody_ServedAs200FromFaviconIco()
    {
        // The most common way the direct rung "succeeds" while returning nothing usable.
        var handler = new QueuedHandler();
        handler.Enqueue(HttpStatusCode.NotFound, Array.Empty<byte>(), "text/plain");
        handler.Enqueue(HttpStatusCode.OK, Encoding.UTF8.GetBytes("<!DOCTYPE html><html>404</html>"), "image/x-icon");
        handler.Enqueue(HttpStatusCode.OK, Png(), "image/png");

        var bytes = await CreateService(handler).GetFaviconAsync("example.com");

        Assert.Equal(Png(), bytes);
        Assert.Equal(3, handler.RequestCount);
    }

    [Fact]
    public async Task RejectsTextContentType_WithoutReadingIt()
    {
        var handler = new QueuedHandler();
        handler.Enqueue(HttpStatusCode.OK, Png(), "text/html");
        handler.Enqueue(HttpStatusCode.OK, Png(), "image/png");

        var bytes = await CreateService(handler).GetFaviconAsync("example.com");

        Assert.Equal(Png(), bytes);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task RejectsSvg_BecauseSkiaCannotDecodeIt()
    {
        var handler = new QueuedHandler();
        handler.Enqueue(HttpStatusCode.OK, Encoding.UTF8.GetBytes("<svg/>"), "image/svg+xml");
        handler.Enqueue(HttpStatusCode.OK, Png(), "image/png");

        var bytes = await CreateService(handler).GetFaviconAsync("example.com");

        Assert.Equal(Png(), bytes);
    }

    [Fact]
    public async Task RejectsOversizeResponse()
    {
        var handler = new QueuedHandler();
        handler.Enqueue(HttpStatusCode.OK, new byte[512 * 1024], "image/png");
        handler.Enqueue(HttpStatusCode.OK, Png(), "image/png");

        var bytes = await CreateService(handler).GetFaviconAsync("example.com");

        Assert.Equal(Png(), bytes);
    }

    [Fact]
    public async Task AcceptsOctetStream_WhenBytesSniffAsAnImage()
    {
        // Plenty of servers mislabel .ico this way; the sniff is what vets it.
        var handler = new QueuedHandler();
        handler.Enqueue(HttpStatusCode.OK, Ico(), "application/octet-stream");

        Assert.Equal(Ico(), await CreateService(handler).GetFaviconAsync("example.com"));
    }

    [Fact]
    public async Task AllRungsFail_ReturnsNull_AndNegativeCacheSuppressesRetry()
    {
        var handler = new QueuedHandler();
        handler.DefaultTo(HttpStatusCode.NotFound);

        var service = CreateService(handler);

        Assert.Null(await service.GetFaviconAsync("nothing.example"));
        var afterFirst = handler.RequestCount;
        Assert.Equal(3, afterFirst);

        Assert.Null(await service.GetFaviconAsync("nothing.example"));
        Assert.Equal(afterFirst, handler.RequestCount); // no further network at all
    }

    [Fact]
    public async Task SecondLookupIsServedFromMemory()
    {
        var handler = new QueuedHandler();
        handler.Enqueue(HttpStatusCode.OK, Png(), "image/png");
        handler.DefaultTo(HttpStatusCode.NotFound);

        var service = CreateService(handler);

        Assert.NotNull(await service.GetFaviconAsync("example.com"));
        Assert.Equal(1, handler.RequestCount);

        Assert.NotNull(await service.GetFaviconAsync("example.com"));
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task HostIsCaseAndWhitespaceNormalised()
    {
        var handler = new QueuedHandler();
        handler.Enqueue(HttpStatusCode.OK, Png(), "image/png");
        handler.DefaultTo(HttpStatusCode.NotFound);

        var service = CreateService(handler);

        Assert.NotNull(await service.GetFaviconAsync("Example.COM"));
        Assert.NotNull(await service.GetFaviconAsync("  example.com  "));

        // Both spellings are one cache entry, not two lookups.
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ConcurrentLookupsForSameHost_ShareOneFetch()
    {
        // A list page full of stories from one host must not fan out into N chains.
        var handler = new QueuedHandler { ArtificialDelay = TimeSpan.FromMilliseconds(50) };
        handler.Enqueue(HttpStatusCode.OK, Png(), "image/png");
        handler.DefaultTo(HttpStatusCode.NotFound);

        var service = CreateService(handler);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => service.GetFaviconAsync("example.com")));

        Assert.All(results, r => Assert.NotNull(r));
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankHost_ReturnsNull_WithoutTouchingTheNetwork(string? host)
    {
        var handler = new QueuedHandler();
        handler.DefaultTo(HttpStatusCode.NotFound);

        Assert.Null(await CreateService(handler).GetFaviconAsync(host!));
        Assert.Equal(0, handler.RequestCount);
    }

    /// <summary>
    /// Returns queued responses in order, then a default, recording every URI requested.
    /// Extends the single-response handler pattern used by <see cref="DigestClientTests"/>
    /// so a whole fallback chain can be scripted in one test.
    /// </summary>
    private sealed class QueuedHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, byte[] Body, string ContentType)> _queued = new();
        private HttpStatusCode? _default;
        private readonly object _gate = new();

        public List<string> RequestedUris { get; } = new();

        public int RequestCount
        {
            get { lock (_gate) return RequestedUris.Count; }
        }

        public TimeSpan ArtificialDelay { get; init; } = TimeSpan.Zero;

        public void Enqueue(HttpStatusCode status, byte[] body, string contentType)
            => _queued.Enqueue((status, body, contentType));

        public void DefaultTo(HttpStatusCode status) => _default = status;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (_gate) RequestedUris.Add(request.RequestUri!.ToString());

            if (ArtificialDelay > TimeSpan.Zero)
                await Task.Delay(ArtificialDelay, cancellationToken);

            (HttpStatusCode Status, byte[] Body, string ContentType) next;
            lock (_gate)
            {
                if (_queued.Count > 0) next = _queued.Dequeue();
                else if (_default is { } d) next = (d, Array.Empty<byte>(), "text/plain");
                else throw new InvalidOperationException("QueuedHandler ran out of scripted responses.");
            }

            var content = new ByteArrayContent(next.Body);
            content.Headers.Remove("Content-Type");
            content.Headers.Add("Content-Type", next.ContentType);

            return new HttpResponseMessage(next.Status) { Content = content };
        }
    }
}
