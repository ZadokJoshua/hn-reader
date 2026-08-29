using HNReader.Core.Models;
using HNReader.Core.Services.Logging;
using HtmlAgilityPack;
using System.Diagnostics;

namespace HNReader.Core.Services;

/// <summary>
/// A faster alternative to the HN Firebase API that parses comments directly from
/// the Hacker News website HTML. This approach is significantly faster because it
/// fetches all comments in a single HTTP request instead of making individual API
/// calls for each comment.
/// </summary>
public class HNWebClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger? _logger;

    public HNWebClient(HttpClient httpClient, ILogger? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Fetches the comment count directly from the HTML page, which is authoritative
    /// even when the HN Firebase API returns 0 (common for Ask HN posts).
    /// </summary>
    /// <param name="storyId">The HN story ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of <c>&lt;tr class="comtr"&gt;</c> rows in the page, or 0 if the request failed.</returns>
    public async Task<int> GetAccurateCommentCountAsync(int storyId, CancellationToken cancellationToken = default)
    {
        try
        {
            var html = await _httpClient.GetStringAsync($"item?id={storyId}", cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            // Count <tr class="comtr"> rows. This is the source of truth — the
            // HTML page always reflects the current state of comments.
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var nodes = doc.DocumentNode.SelectNodes("//tr[contains(@class,'comtr')]");
            var count = nodes?.Count ?? 0;
            _logger?.LogDebug("HNWeb", "comment count fetched",
                context: ContextOf(("storyId", storyId), ("count", count)));
            return count;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("HNWeb", "comment count fetch failed", ex,
                context: ContextOf(("storyId", storyId)));
            return 0;
        }
    }

    /// <summary>
    /// Fetches all comments for a story by parsing the HTML page directly.
    /// This is much faster than the API approach as it requires only one HTTP request.
    /// </summary>
    /// <param name="storyId">The HN story ID</param>
    /// <returns>A list of comments with their depth information preserved</returns>
    public async Task<List<WebComment>> GetCommentsFromWebAsync(int storyId, CancellationToken cancellationToken = default)
    {
        var (comments, _) = await FetchCommentsPageAsync(storyId, cancellationToken).ConfigureAwait(false);
        return comments;
    }

    /// <summary>
    /// Fetches the comments page once and returns both the parsed comment list and
    /// the authoritative <c>tr.comtr</c> row count from that same document — the
    /// combined call callers should prefer over calling <see cref="GetCommentsFromWebAsync"/>
    /// and <see cref="GetAccurateCommentCountAsync"/> separately, which would download
    /// and parse the identical page twice.
    /// </summary>
    public async Task<(List<WebComment> Comments, int AccurateCount)> FetchCommentsPageAsync(int storyId, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var comments = new List<WebComment>();

        try
        {
            var html = await _httpClient.GetStringAsync($"item?id={storyId}", cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Get all comment rows using XPath selectors
            var commentNodes = doc.DocumentNode.SelectNodes("//tr[contains(@class,'comtr')]");
            var accurateCount = commentNodes?.Count ?? 0;
            if (commentNodes == null || commentNodes.Count == 0) return (comments, accurateCount);

            foreach (var commentNode in commentNodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var comment = ParseCommentFromNode(commentNode);
                if (comment != null) comments.Add(comment);
            }

            sw.Stop();
            _logger?.LogInformation("HNWeb", "comments parsed",
                context: ContextOf(("storyId", storyId), ("count", comments.Count), ("durationMs", sw.ElapsedMilliseconds)));
            return (comments, accurateCount);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger?.LogError("HNWeb", "comments fetch failed", ex,
                context: ContextOf(("storyId", storyId), ("durationMs", sw.ElapsedMilliseconds)));
            throw;
        }
    }

    private WebComment? ParseCommentFromNode(HtmlNode commentNode)
    {
        try
        {
            // Get comment ID
            var idAttr = commentNode.Id;
            if (string.IsNullOrEmpty(idAttr) || !int.TryParse(idAttr, out var id)) return null;

            // Parse depth (indent level)
            var indentNode = commentNode.SelectSingleNode(".//td[contains(@class,'ind')]");
            var depth = 0;
            if (indentNode != null
                && indentNode.GetAttributeValue("indent", string.Empty) is string indentStr
                && int.TryParse(indentStr, out var d))
            {
                depth = d;
            }

            // Parse author
            var authorNode = commentNode.SelectSingleNode(".//a[contains(@class,'hnuser')]");
            if (authorNode == null) return null;
            var author = authorNode.InnerText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(author)) return null;

            // Parse timestamp
            var ageNode = commentNode.SelectSingleNode(".//span[contains(@class,'age')]");
            var timestamp = ageNode?.GetAttributeValue("title", string.Empty);

            // Parse comment text
            var commentTextNode = commentNode.SelectSingleNode(".//div[contains(@class,'commtext')]");
            if (commentTextNode == null) return null;
            var text = CleanCommentTextOptimized(commentTextNode);

            // Check if comment is deleted or dead
            // Cache OuterHtml to avoid multiple string rebuilds
            var outerHtml = commentNode.OuterHtml;
            if (string.IsNullOrWhiteSpace(text) ||
                outerHtml.Contains("[deleted]") ||
                outerHtml.Contains("[flagged]") ||
                outerHtml.Contains("class=\"cdd\"") ||
                outerHtml.Contains("[dead]"))
            {
                return null;
            }

            return new WebComment
            {
                Id = id,
                By = author,
                Text = text,
                Depth = depth,
                TimeString = timestamp,
                Time = ParseTimestamp(timestamp)
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("HNWeb", "comment parse failed", ex);
            return null;
        }
    }

    private static string CleanCommentTextOptimized(HtmlNode commentTextNode)
    {
        // Remove reply links and parent comment references using DOM manipulation
        var replyLinks = commentTextNode.SelectNodes(".//a[contains(@href,'reply')]");
        if (replyLinks != null)
            foreach (var link in replyLinks) link.Remove();

        var parNodes = commentTextNode.SelectNodes(".//span[contains(@class,'par')]");
        if (parNodes != null)
            foreach (var node in parNodes) node.Remove();

        // Get cleaned HTML directly without creating a new document
        var cleanedHtml = commentTextNode.InnerHtml;
        return cleanedHtml.Trim();
    }

    private static long ParseTimestamp(string? timestampStr)
    {
        if (string.IsNullOrEmpty(timestampStr)) return 0;

        // HN timestamp format: "2026-01-27T19:04:50 1769540690" (ISO date + Unix timestamp)
        // We'll use the Unix timestamp for accuracy
        var parts = timestampStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Try to parse the Unix timestamp (second part)
        if (parts.Length >= 2 && long.TryParse(parts[1], out var unixTimestamp))
            return unixTimestamp;

        // Fallback: try to parse the ISO date (first part). DateTime.TryParse can
        // return a DateTime whose Kind doesn't match TimeSpan.Zero (e.g. Local),
        // which makes the DateTimeOffset constructor throw — force Utc so the
        // offset is always valid regardless of the parsed Kind.
        if (parts.Length >= 1 && DateTime.TryParse(parts[0], out var dateTime))
            return new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc), TimeSpan.Zero).ToUnixTimeSeconds();

        return 0;
    }

    private static IReadOnlyList<KeyValuePair<string, object?>> ContextOf(
        params (string Key, object? Value)[] pairs)
    {
        var list = new List<KeyValuePair<string, object?>>(pairs.Length);
        foreach (var (k, v) in pairs) list.Add(new KeyValuePair<string, object?>(k, v));
        return list;
    }
}
