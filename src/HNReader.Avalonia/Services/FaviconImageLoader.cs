using System;
using System.IO;
using System.Threading.Tasks;
using AsyncImageLoader;
using AsyncImageLoader.Loaders;
using Avalonia.Media.Imaging;
using HNReader.Core.Constants;
using HNReader.Core.Helpers;
using HNReader.Core.Interfaces;
using HNReader.Core.Services.Logging;

namespace HNReader.Avalonia.Services;

/// <summary>
/// The application-wide <see cref="IAsyncImageLoader"/>.
///
/// <para>
/// <see cref="ImageLoader.AsyncImageLoader"/> is a single global hook, and this app uses
/// the attached <c>ImageLoader.Source</c> property for two unrelated things: story
/// favicons and the digest page's article preview images. A loader that assumed every
/// request was a favicon would silently break the previews. So favicon requests are
/// marked with the <see cref="Scheme"/> sentinel and everything else is handed to a stock
/// web loader untouched.
/// </para>
/// </summary>
public class FaviconImageLoader : IAsyncImageLoader
{
    /// <summary>
    /// Marks a request as "resolve this host through <see cref="IFaviconService"/>" rather
    /// than "download this URL". Not a real URL scheme - it never reaches the network.
    /// </summary>
    public const string Scheme = "hnfavicon://";

    private const string LogCategory = "Favicon";

    private readonly IFaviconService _faviconService;
    private readonly IAsyncImageLoader _inner;
    private readonly ILogger? _logger;

    // Decoding is not free and a list page repeats hosts constantly, so decoded bitmaps
    // are kept as well as the raw bytes.
    private readonly LRUCache<string, Bitmap> _bitmapCache =
        new(AppFileNames.FAVICON_BITMAP_CACHE_SIZE);

    public FaviconImageLoader(IFaviconService faviconService, ILogger? logger = null)
    {
        _faviconService = faviconService ?? throw new ArgumentNullException(nameof(faviconService));
        _logger = logger;
        // DiskCachedWebImageLoader specifically, because that is what the library uses
        // when nothing is assigned - which was the case before this loader existed. Using
        // the RAM-only loader here would quietly cost the digest previews their on-disk
        // cache and re-download them every launch.
        _inner = new DiskCachedWebImageLoader();
    }

    /// <summary>Builds the sentinel value bound to <c>ImageLoader.Source</c>.</summary>
    public static string? ToSource(string? faviconHost) =>
        string.IsNullOrWhiteSpace(faviconHost) ? null : Scheme + faviconHost;

    public Task<Bitmap?> ProvideImageAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return Task.FromResult<Bitmap?>(null);

        return url.StartsWith(Scheme, StringComparison.Ordinal)
            ? LoadFaviconAsync(url.Substring(Scheme.Length))
            : _inner.ProvideImageAsync(url);
    }

    private async Task<Bitmap?> LoadFaviconAsync(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return null;

        if (_bitmapCache.TryGetValue(host, out var cached) && cached is not null)
            return cached;

        var bytes = await _faviconService.GetFaviconAsync(host).ConfigureAwait(false);
        if (bytes is null || bytes.Length == 0) return null;

        try
        {
            using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);

            // Deliberately not disposing on eviction: an evicted bitmap may still be
            // assigned to a live Image.Source, and disposing it crashes the next render.
            // Let the GC collect it once nothing references it.
            _bitmapCache.Set(host, bitmap);
            return bitmap;
        }
        catch (Exception ex)
        {
            // Last line of defence past the service's content-type and magic-byte checks.
            // The caller shows the globe, so this is a log line rather than a failure.
            _logger?.LogWarning(LogCategory, "Favicon bytes could not be decoded.", ex,
                new[] { new System.Collections.Generic.KeyValuePair<string, object?>("host", host) });
            return null;
        }
    }

    public void Dispose()
    {
        _inner.Dispose();
        _bitmapCache.Clear();
        GC.SuppressFinalize(this);
    }
}
