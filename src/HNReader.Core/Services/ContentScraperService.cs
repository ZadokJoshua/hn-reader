using HNReader.Core.Interfaces;
using HtmlAgilityPack;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace HNReader.Core.Services;

/// <summary>
/// Implementation of <see cref="IContentScraperService"/> that uses HtmlAgilityPack
/// to extract plain text from web pages with robots.txt compliance.
/// </summary>
public class ContentScraperService(HttpClient httpClient) : IContentScraperService
{
    private const int MaxCharacters = 5000;
    private const string UserAgentIdentifier = "HNReader"; // Should match User-Agent configured in DI

    // Cache robots.txt results to avoid repeated requests (domain -> allowed paths)
    private static readonly ConcurrentDictionary<string, RobotsTxtCache> _robotsCache = new();

    // Cache extracted image URLs per article URL (populated during GetPlainTextAsync)
    private static readonly ConcurrentDictionary<string, string?> _imageUrlCache = new();

    // Tags whose entire subtree should be removed before extracting text
    private static readonly string[] RemovableTags =
    [
        "script", "style", "nav", "footer", "header",
        "aside", "noscript", "iframe", "svg", "form"
    ];

    /// <inheritdoc />
    public async Task<string> GetPlainTextAsync(string url)
    {
        try
        {
            // Check robots.txt first
            if (!await IsAllowedByRobotsTxtAsync(url))
            {
                Debug.WriteLine($"[ContentScraper] Blocked by robots.txt: {url}");
                return "[Error: Scraping not allowed by site's robots.txt]";
            }

            var html = await httpClient.GetStringAsync(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Extract and cache the first image URL before removing tags
            var imageUrl = ExtractFirstImageUrl(doc, url);
            _imageUrlCache[url] = imageUrl;
            if (imageUrl is not null)
            {
                Debug.WriteLine($"[ContentScraper] Cached image for {url}: {imageUrl}");
            }

            // Remove non-content nodes
            foreach (var tag in RemovableTags)
            {
                var nodes = doc.DocumentNode.SelectNodes($"//{tag}");
                if (nodes is null) continue;

                foreach (var node in nodes)
                {
                    node.Remove();
                }
            }

            // Extract and normalise visible text
            var rawText = HtmlEntity.DeEntitize(doc.DocumentNode.InnerText);

            // Collapse whitespace runs into single spaces and trim blank lines
            var lines = rawText
                .Split('\n', StringSplitOptions.TrimEntries)
                .Where(l => l.Length > 0)
                .Select(l => string.Join(' ', l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));

            var plainText = string.Join('\n', lines).Trim();

            if (plainText.Length > MaxCharacters)
            {
                plainText = plainText[..MaxCharacters] + "\n[truncated]";
            }

            Debug.WriteLine($"[ContentScraper] Scraped {url} → {plainText.Length} chars");
            return plainText;
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[ContentScraper] HTTP error scraping {url}: {ex.Message}");
            return $"[Error: HTTP request failed - {ex.Message}]";
        }
        catch (TaskCanceledException ex)
        {
            Debug.WriteLine($"[ContentScraper] Timeout scraping {url}: {ex.Message}");
            return "[Error: Request timeout - site took too long to respond]";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContentScraper] Unexpected error scraping {url}: {ex.GetType().Name} - {ex.Message}");
            return $"[Error: Failed to scrape article - {ex.Message}]";
        }
    }

    /// <inheritdoc />
    public bool TryGetCachedImageUrl(string url, out string? imageUrl)
    {
        return _imageUrlCache.TryGetValue(url, out imageUrl);
    }

    /// <summary>
    /// Extracts the first relevant image URL from the HTML document.
    /// Prioritizes Open Graph image, then falls back to first img in article/main.
    /// </summary>
    private static string? ExtractFirstImageUrl(HtmlDocument doc, string baseUrl)
    {
        try
        {
            // Try og:image meta tag first (best for article thumbnails)
            var ogImageNode = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
            var ogImageUrl = ogImageNode?.GetAttributeValue("content", string.Empty);
            if (!string.IsNullOrWhiteSpace(ogImageUrl))
            {
                return NormalizeImageUrl(ogImageUrl, baseUrl);
            }

            // Fallback: first img within article or main content areas
            var imgNode = doc.DocumentNode.SelectSingleNode("//article//img[@src] | //main//img[@src] | //div[contains(@class,'content')]//img[@src]");
            var imgSrc = imgNode?.GetAttributeValue("src", string.Empty);
            if (!string.IsNullOrWhiteSpace(imgSrc))
            {
                return NormalizeImageUrl(imgSrc, baseUrl);
            }

            // Last resort: first img tag anywhere
            var anyImg = doc.DocumentNode.SelectSingleNode("//img[@src]");
            var anySrc = anyImg?.GetAttributeValue("src", string.Empty);
            if (!string.IsNullOrWhiteSpace(anySrc))
            {
                return NormalizeImageUrl(anySrc, baseUrl);
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ContentScraper] Error extracting image: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Converts relative image URLs to absolute URLs.
    /// </summary>
    private static string? NormalizeImageUrl(string imageUrl, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        // Skip data URIs and invalid URLs
        if (imageUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return null;

        // Already absolute
        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var absoluteUri))
        {
            return IsSupportedImageScheme(absoluteUri) ? absoluteUri.ToString() : null;
        }

        // Convert relative to absolute
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) &&
            Uri.TryCreate(baseUri, imageUrl, out var resolvedUri))
        {
            return IsSupportedImageScheme(resolvedUri) ? resolvedUri.ToString() : null;
        }

        return null;
    }

    private static bool IsSupportedImageScheme(Uri uri)
    {
        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
               || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the URL is allowed by the site's robots.txt file.
    /// </summary>
    private async Task<bool> IsAllowedByRobotsTxtAsync(string url)
    {
        try
        {
            var uri = new Uri(url);
            var domain = $"{uri.Scheme}://{uri.Host}";
            var robotsTxtUrl = $"{domain}/robots.txt";

            // Check cache first
            if (_robotsCache.TryGetValue(domain, out var cached) && !cached.IsExpired)
            {
                return cached.IsPathAllowed(uri.PathAndQuery, UserAgentIdentifier);
            }

            // Fetch robots.txt
            var response = await httpClient.GetAsync(robotsTxtUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                // No robots.txt or 404 = allowed by default
                Debug.WriteLine($"[ContentScraper] No robots.txt at {domain}, allowing by default");
                _robotsCache[domain] = RobotsTxtCache.AllowAll();
                return true;
            }

            var robotsTxtContent = await response.Content.ReadAsStringAsync();
            var cache = RobotsTxtCache.Parse(robotsTxtContent);
            _robotsCache[domain] = cache;

            return cache.IsPathAllowed(uri.PathAndQuery, UserAgentIdentifier);
        }
        catch (Exception ex)
        {
            // On error, allow by default but log
            Debug.WriteLine($"[ContentScraper] Error checking robots.txt for {url}: {ex.Message}");
            return true;
        }
    }

    /// <summary>
    /// Simple robots.txt parser and cache with expiration.
    /// </summary>
    private class RobotsTxtCache
    {
        private readonly List<string> _disallowedPaths = [];
        private readonly DateTime _expiresAt;

        private RobotsTxtCache(List<string> disallowedPaths, TimeSpan cacheDuration)
        {
            _disallowedPaths = disallowedPaths;
            _expiresAt = DateTime.UtcNow.Add(cacheDuration);
        }

        public bool IsExpired => DateTime.UtcNow > _expiresAt;

        public static RobotsTxtCache AllowAll() => new([], TimeSpan.FromHours(1));

        public static RobotsTxtCache Parse(string content)
        {
            var disallowedPaths = new List<string>();
            var lines = content.Split('\n', StringSplitOptions.TrimEntries);
            var isRelevantSection = false;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                    continue;

                // Check for User-agent directive
                if (line.StartsWith("User-agent:", StringComparison.OrdinalIgnoreCase))
                {
                    var userAgent = line[11..].Trim();
                    // Match our user agent or wildcard
                    isRelevantSection = userAgent.Equals("*", StringComparison.Ordinal) ||
                                      userAgent.Equals(UserAgentIdentifier, StringComparison.OrdinalIgnoreCase);
                }
                else if (isRelevantSection && line.StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase))
                {
                    var path = line[9..].Trim();
                    if (!string.IsNullOrEmpty(path))
                    {
                        disallowedPaths.Add(path);
                    }
                }
            }

            return new RobotsTxtCache(disallowedPaths, TimeSpan.FromHours(24));
        }

        public bool IsPathAllowed(string path, string userAgent)
        {
            // Empty disallow list = everything allowed
            if (_disallowedPaths.Count == 0)
                return true;

            // Check if path matches any disallowed pattern
            foreach (var disallowed in _disallowedPaths)
            {
                if (disallowed == "/")
                    return false; // Disallow all

                if (path.StartsWith(disallowed, StringComparison.Ordinal))
                    return false;
            }

            return true;
        }
    }
}
