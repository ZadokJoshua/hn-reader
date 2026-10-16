using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HNReader.Core.Constants;
using HNReader.Core.Helpers;
using HNReader.Core.Interfaces;
using HNReader.Core.Services.Logging;

namespace HNReader.Core.Services;

/// <summary>
/// Resolves a host to favicon bytes.
///
/// Previously the app asked Google's s2 service for a truncated domain and showed nothing
/// when that missed, with no logging to say why. Three things changed: callers now pass
/// the real host, several providers are tried in order, and every miss is recorded.
/// See <see cref="AppFileNames"/> for why Google is tried last.
/// </summary>
public class FaviconService : IFaviconService
{
    private const string LogCategory = "Favicon";

    private readonly HttpClient _httpClient;
    private readonly string? _diskCacheDirectory;
    private readonly ILogger? _logger;

    private readonly LRUCache<string, byte[]> _positiveCache =
        new(AppFileNames.FAVICON_MEMORY_CACHE_SIZE, AppFileNames.FAVICON_POSITIVE_TTL);

    // The value is unused; only presence and expiry matter. A negative entry is what stops
    // a genuinely icon-less site from re-running the whole chain on every scroll.
    private readonly LRUCache<string, byte> _negativeCache =
        new(AppFileNames.FAVICON_MEMORY_CACHE_SIZE, AppFileNames.FAVICON_NEGATIVE_TTL);

    // A list page shows many stories from one host. Without this, every row starts its own
    // identical chain.
    private readonly ConcurrentDictionary<string, Task<byte[]?>> _inFlight = new();

    public FaviconService(HttpClient httpClient, string? diskCacheDirectory = null, ILogger? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _diskCacheDirectory = diskCacheDirectory;
        _logger = logger;

        TryPrepareDiskCache();
    }

    public Task<byte[]?> GetFaviconAsync(string faviconHost, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(faviconHost)) return Task.FromResult<byte[]?>(null);

        var host = faviconHost.Trim().ToLowerInvariant();

        if (_positiveCache.TryGetValue(host, out var cached) && cached is not null)
            return Task.FromResult<byte[]?>(cached);

        if (_negativeCache.ContainsKey(host))
            return Task.FromResult<byte[]?>(null);

        // GetOrAdd's factory may run more than once under contention, but only one result is
        // stored, so every caller still awaits a single shared task.
        return _inFlight.GetOrAdd(host, h => ResolveAndCacheAsync(h, cancellationToken));
    }

    private async Task<byte[]?> ResolveAndCacheAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            var fromDisk = TryReadDisk(host);
            if (fromDisk is not null)
            {
                _positiveCache.Set(host, fromDisk);
                return fromDisk;
            }

            using var chainCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            chainCts.CancelAfter(TimeSpan.FromSeconds(AppFileNames.FAVICON_CHAIN_TIMEOUT_SECONDS));

            var bytes = await FetchThroughChainAsync(host, chainCts.Token).ConfigureAwait(false);

            if (bytes is not null)
            {
                _positiveCache.Set(host, bytes);
                TryWriteDisk(host, bytes);
            }
            else
            {
                _negativeCache.Set(host, 0);
                _logger?.LogWarning(LogCategory,
                    "No usable favicon found; falling back to the globe placeholder.",
                    context: new[] { new KeyValuePair<string, object?>("host", host) });
            }

            return bytes;
        }
        catch (Exception ex)
        {
            // Never let a missing icon take down a story row.
            _negativeCache.Set(host, 0);
            _logger?.LogWarning(LogCategory, "Favicon lookup failed.", ex,
                new[] { new KeyValuePair<string, object?>("host", host) });
            return null;
        }
        finally
        {
            _inFlight.TryRemove(host, out _);
        }
    }

    private async Task<byte[]?> FetchThroughChainAsync(string host, CancellationToken cancellationToken)
    {
        string[] urls =
        {
            string.Format(AppFileNames.FAVICON_DUCKDUCKGO_URL_FORMAT, host),
            string.Format(AppFileNames.FAVICON_DIRECT_URL_FORMAT, host),
            string.Format(AppFileNames.FAVICON_GOOGLE_URL_FORMAT, Uri.EscapeDataString(host)),
        };

        foreach (var url in urls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bytes = await TryFetchAsync(url, cancellationToken).ConfigureAwait(false);
            if (bytes is not null) return bytes;
        }

        return null;
    }

    private async Task<byte[]?> TryFetchAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var rungCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            rungCts.CancelAfter(TimeSpan.FromSeconds(AppFileNames.FAVICON_RUNG_TIMEOUT_SECONDS));

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, rungCts.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode) return null;

            // Check the declared length before reading a byte of the body.
            if (response.Content.Headers.ContentLength is > AppFileNames.FAVICON_MAX_BYTES) return null;

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!IsPlausibleImageMediaType(mediaType)) return null;

            var bytes = await ReadCappedAsync(response, rungCts.Token).ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0) return null;

            return LooksLikeSupportedImage(bytes) ? bytes : null;
        }
        catch (Exception)
        {
            // Timeout, DNS failure, TLS error, cancellation - all mean "try the next rung".
            return null;
        }
    }

    /// <summary>
    /// Rejects media types that cannot be a bitmap we are able to decode.
    ///
    /// SVG is called out explicitly: it is a legitimate favicon format on the web, but
    /// Avalonia's Skia codecs cannot decode it, so accepting it would only fail later and
    /// more obscurely. <c>application/octet-stream</c> is allowed through because plenty of
    /// servers mislabel .ico that way - the magic-byte check is what actually vets those.
    /// </summary>
    private static bool IsPlausibleImageMediaType(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType)) return true; // no header: let the sniff decide

        if (mediaType.Equals("image/svg+xml", StringComparison.OrdinalIgnoreCase)) return false;
        if (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)) return false;
        if (mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)) return false;
        if (mediaType.Equals("application/xml", StringComparison.OrdinalIgnoreCase)) return false;

        return true;
    }

    private static async Task<byte[]?> ReadCappedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();

        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            buffer.Write(chunk, 0, read);

            // A server can lie about (or omit) Content-Length, so cap the actual read too.
            if (buffer.Length > AppFileNames.FAVICON_MAX_BYTES) return null;
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Magic-byte check for the formats Skia can decode.
    ///
    /// The highest-value case is an HTML error page served as <c>200 OK</c> from
    /// <c>/favicon.ico</c> - the most common way the direct rung "succeeds" while returning
    /// nothing usable.
    /// </summary>
    private static bool LooksLikeSupportedImage(byte[] bytes)
    {
        if (bytes.Length < 4) return false;

        // Reject markup first, including a UTF-8 BOM or leading space before the '<'.
        var start = 0;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) start = 3;
        while (start < bytes.Length && (bytes[start] == 0x20 || bytes[start] == 0x09 ||
                                        bytes[start] == 0x0A || bytes[start] == 0x0D)) start++;
        if (start < bytes.Length && bytes[start] == 0x3C) return false; // '<'

        // PNG
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return true;
        // ICO / CUR
        if (bytes[0] == 0x00 && bytes[1] == 0x00 && (bytes[2] == 0x01 || bytes[2] == 0x02) && bytes[3] == 0x00) return true;
        // GIF
        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46) return true;
        // JPEG
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return true;
        // BMP
        if (bytes[0] == 0x42 && bytes[1] == 0x4D) return true;
        // RIFF....WEBP
        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) return true;

        return false;
    }

    // ---- Disk tier -----------------------------------------------------
    //
    // Survives restarts, so a previously-seen list paints with no network at all.
    // Every operation here is best-effort: a cache is never worth failing a render over.

    private void TryPrepareDiskCache()
    {
        if (string.IsNullOrWhiteSpace(_diskCacheDirectory)) return;

        try
        {
            Directory.CreateDirectory(_diskCacheDirectory);
            PurgeExpiredDiskEntries();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(LogCategory, "Favicon disk cache unavailable; running memory-only.", ex);
        }
    }

    private void PurgeExpiredDiskEntries()
    {
        if (string.IsNullOrWhiteSpace(_diskCacheDirectory)) return;

        var cutoff = DateTime.UtcNow - AppFileNames.FAVICON_DISK_TTL;

        foreach (var file in Directory.EnumerateFiles(_diskCacheDirectory, "*.bin"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file);
            }
            catch
            {
                // Locked or already gone - the next startup can try again.
            }
        }
    }

    private string? DiskPathFor(string host)
    {
        if (string.IsNullOrWhiteSpace(_diskCacheDirectory)) return null;

        // Hash rather than use the host verbatim: hosts contain characters that are legal in
        // a domain but not in a file name, and this bounds the length.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(host));
        return Path.Combine(_diskCacheDirectory, Convert.ToHexString(hash).ToLowerInvariant() + ".bin");
    }

    private byte[]? TryReadDisk(string host)
    {
        var path = DiskPathFor(host);
        if (path is null) return null;

        try
        {
            if (!File.Exists(path)) return null;

            if (File.GetLastWriteTimeUtc(path) < DateTime.UtcNow - AppFileNames.FAVICON_DISK_TTL)
            {
                File.Delete(path);
                return null;
            }

            var bytes = File.ReadAllBytes(path);
            return bytes.Length > 0 ? bytes : null;
        }
        catch
        {
            return null;
        }
    }

    private void TryWriteDisk(string host, byte[] bytes)
    {
        var path = DiskPathFor(host);
        if (path is null) return;

        try
        {
            // Write-then-move so a crash mid-write cannot leave a truncated icon behind.
            var temp = path + ".tmp";
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            // The memory cache still holds it for this session.
        }
    }
}
