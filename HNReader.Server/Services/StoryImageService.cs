using System.Text.RegularExpressions;

namespace HNReader.Server.Services;

/// <summary>
/// Best-effort preview-image lookup for a story's own page, purely to let
/// clients render a thumbnail. Never throws and never blocks a digest run: a
/// missing image is the expected outcome for plenty of pages, not an error.
///
/// <para>
/// Results distinguish "this page has no usable image" from "we could not reach
/// this page", because the caller caches the outcome. Collapsing the two was
/// measured doing real damage: a transient DNS failure during one run made all
/// 27 lookups fail, and recording those as settled would have permanently
/// blanked the preview image for every one of those stories. Only a conclusive
/// answer is worth remembering.
/// </para>
///
/// <para>
/// Deliberately not built on SmartReader (which backs scrape_article and does
/// expose a featured image): a full readability parse of every story in the
/// digest costs far more than this needs to. Open Graph/Twitter-card tags live
/// in &lt;head&gt;, so this reads only the first <see cref="MaxBytesToScan"/>
/// bytes of the response and stops — enough to find the tag, cheap enough to run
/// over every story in the digest concurrently.
/// </para>
///
/// <para>
/// HN's own pages are skipped by the caller, not here: they have no preview
/// image worth showing, and this exists to enrich the *article* a story points
/// at.
/// </para>
/// </summary>
public class StoryImageService
{
    /// <summary>
    /// How much of the response body to read before giving up on finding a tag.
    /// og:image is a &lt;head&gt; element, so 128KB covers even pages with large
    /// inline &lt;style&gt; blocks ahead of it, while bounding what a hostile or
    /// simply enormous page can cost us.
    /// </summary>
    private const int MaxBytesToScan = 128 * 1024;

    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(8);

    // Attribute order varies in the wild (content before property is common), so
    // both orders are matched rather than assuming the canonical spelling.
    private static readonly Regex[] ImageTagPatterns =
    [
        new(@"<meta[^>]+(?:property|name)\s*=\s*[""'](?:og:image(?::secure_url|:url)?|twitter:image(?::src)?)[""'][^>]+content\s*=\s*[""']([^""']+)[""']",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"<meta[^>]+content\s*=\s*[""']([^""']+)[""'][^>]+(?:property|name)\s*=\s*[""'](?:og:image(?::secure_url|:url)?|twitter:image(?::src)?)[""']",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"<link[^>]+rel\s*=\s*[""']image_src[""'][^>]+href\s*=\s*[""']([^""']+)[""']",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    private readonly HttpClient _httpClient;
    private readonly ILogger<StoryImageService> _logger;

    public StoryImageService(HttpClient httpClient, ILogger<StoryImageService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Returns an absolute preview image URL for the page at <paramref name="url"/>,
    /// or null if the page has none, isn't reachable, or isn't a usable target.
    /// </summary>
    public async Task<PreviewImageResult> TryGetPreviewImageAsync(
        string? url, CancellationToken cancellationToken = default)
    {
        // Nothing to fetch and nothing that will ever change about that, so this
        // counts as a settled answer rather than a failure to retry later.
        if (!IsEligible(url, out var pageUri)) return PreviewImageResult.NoImage;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(FetchTimeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, pageUri);
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                .ConfigureAwait(false);

            // A 5xx or a rate-limited response may well succeed later; a 404 or
            // a 403 is the page telling us there is nothing here for us.
            if (!response.IsSuccessStatusCode)
            {
                return (int)response.StatusCode >= 500 || (int)response.StatusCode == 429
                    ? PreviewImageResult.Unreachable
                    : PreviewImageResult.NoImage;
            }

            // A PDF or image response has no meta tags to scan; bail before
            // pulling 128KB of a binary body through the regexes.
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is not null && !mediaType.Contains("html", StringComparison.OrdinalIgnoreCase))
            {
                return PreviewImageResult.NoImage;
            }

            var head = await ReadHeadAsync(response, cts.Token).ConfigureAwait(false);
            var candidate = ExtractImageUrl(head);
            if (candidate is null) return PreviewImageResult.NoImage;

            // Resolve against the *final* URI so a redirected page's relative
            // og:image doesn't get resolved against the pre-redirect host.
            var baseUri = response.RequestMessage?.RequestUri ?? pageUri;
            var normalized = Normalize(candidate, baseUri);
            return normalized is null ? PreviewImageResult.NoImage : PreviewImageResult.Found(normalized);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A cancelled digest run should stop fetching, not work through the
            // rest of the list producing nulls.
            throw;
        }
        catch (Exception ex)
        {
            // Timeouts, DNS failures, TLS errors: all worth another try on a
            // future run, so this must not be cached as a settled "no image".
            _logger.LogDebug(ex, "Digest: preview image lookup failed for {Url}", url);
            return PreviewImageResult.Unreachable;
        }
    }

    private static bool IsEligible(string? url, out Uri pageUri)
    {
        pageUri = null!;

        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) return false;

        pageUri = parsed;
        return true;
    }

    private static async Task<string> ReadHeadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        var buffer = new byte[MaxBytesToScan];
        var filled = 0;

        while (filled < buffer.Length)
        {
            var read = await stream
                .ReadAsync(buffer.AsMemory(filled, buffer.Length - filled), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0) break;
            filled += read;
        }

        return System.Text.Encoding.UTF8.GetString(buffer, 0, filled);
    }

    private static string? ExtractImageUrl(string html)
    {
        foreach (var pattern in ImageTagPatterns)
        {
            var match = pattern.Match(html);
            if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
            {
                return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
            }
        }

        return null;
    }

    private static string? Normalize(string candidate, Uri baseUri)
    {
        if (!Uri.TryCreate(baseUri, candidate, out var resolved)) return null;

        // Protocol-relative and relative tag values are common and fine, but a
        // data: or javascript: value is not something a client should be handed.
        return resolved.Scheme == Uri.UriSchemeHttp || resolved.Scheme == Uri.UriSchemeHttps
            ? resolved.ToString()
            : null;
    }
}

/// <summary>
/// Outcome of one preview-image lookup. <see cref="Conclusive"/> is what makes
/// the result cacheable: false means the page could not be reached and the
/// question is still open, so a later run should ask again rather than treating
/// the story as image-less forever.
/// </summary>
public readonly record struct PreviewImageResult(string? ImageUrl, bool Conclusive)
{
    /// <summary>The page was read and has no usable preview image.</summary>
    public static readonly PreviewImageResult NoImage = new(null, true);

    /// <summary>The page could not be read; the answer is unknown, not "none".</summary>
    public static readonly PreviewImageResult Unreachable = new(null, false);

    public static PreviewImageResult Found(string imageUrl) => new(imageUrl, true);
}
