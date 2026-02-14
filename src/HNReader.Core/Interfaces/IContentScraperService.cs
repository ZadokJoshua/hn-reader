namespace HNReader.Core.Interfaces;

/// <summary>
/// Scrapes external web pages and returns their main content as plain text.
/// Used by the digest agent to produce content-based story summaries.
/// </summary>
/// <remarks>
/// Implementations respect robots.txt files and will refuse to scrape disallowed paths.
/// Results are cached to minimize repeated requests to the same domains.
/// </remarks>
public interface IContentScraperService
{
    /// <summary>
    /// Fetches the HTML at <paramref name="url"/>, strips non-content elements,
    /// and returns the visible text truncated to approximately 5,000 characters.
    /// Also caches the first image URL found in the page for later retrieval.
    /// </summary>
    /// <param name="url">The URL to scrape</param>
    /// <returns>Plain text content, or an error message if scraping fails</returns>
    Task<string> GetPlainTextAsync(string url);

    /// <summary>
    /// Attempts to retrieve a cached image URL that was extracted during a previous
    /// call to <see cref="GetPlainTextAsync"/>.
    /// </summary>
    /// <param name="url">The page URL to look up</param>
    /// <param name="imageUrl">The cached image URL if found</param>
    /// <returns>True if an image URL was cached for this URL</returns>
    bool TryGetCachedImageUrl(string url, out string? imageUrl);
}
