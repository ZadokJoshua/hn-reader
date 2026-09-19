using SmartReader;

namespace HNReader.Server.Services;

/// <summary>
/// Backs the digest agent's "scrape_article" tool. Never throws — a tool result
/// the agent can always use (even a "content unavailable" string) is more useful
/// than a failed tool call it has to reason about.
/// </summary>
public class ArticleScrapingService
{
    private const int MaxContentChars = 2000;
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(10);

    private readonly ILogger<ArticleScrapingService> _logger;

    public ArticleScrapingService(ILogger<ArticleScrapingService> logger)
    {
        _logger = logger;
    }

    /// <summary>Fetches a web page and extracts its main readable text.</summary>
    /// <param name="url">The article URL to fetch and read.</param>
    public async Task<string> ScrapeArticleAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(FetchTimeout);
            var article = await Reader.ParseArticleAsync(url).WaitAsync(cts.Token).ConfigureAwait(false);

            if (!article.IsReadable || string.IsNullOrWhiteSpace(article.TextContent))
            {
                return "Content unavailable: the page could not be parsed into readable article text.";
            }

            var text = article.TextContent.Trim();
            return text.Length > MaxContentChars
                ? text[..MaxContentChars] + "… [truncated]"
                : text;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A cancelled digest run should stop scraping, not keep burning
            // fetches and report "content unavailable" for every remaining story.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Digest: scrape_article failed for {Url}", url);
            return "Content unavailable: failed to fetch or parse this page.";
        }
    }
}
