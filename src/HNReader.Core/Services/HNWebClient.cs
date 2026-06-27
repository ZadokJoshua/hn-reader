using HNReader.Core.Models;
using HtmlAgilityPack;
using System.Diagnostics;

namespace HNReader.Core.Services;

/// <summary>
/// A faster alternative to the HN Firebase API that parses comments directly from
/// the Hacker News website HTML. This approach is significantly faster because it
/// fetches all comments in a single HTTP request instead of making individual API
/// calls for each comment.
/// </summary>
public class HNWebClient(HttpClient httpClient)
{
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
            var html = await httpClient.GetStringAsync($"item?id={storyId}", cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            // Count <tr class="comtr"> rows. This is the source of truth — the
            // HTML page always reflects the current state of comments.
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var nodes = doc.DocumentNode.SelectNodes("//tr[contains(@class,'comtr')]");
            return nodes?.Count ?? 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error fetching accurate comment count for story {storyId}: {ex.Message}");
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
        var comments = new List<WebComment>();

        var html = await httpClient.GetStringAsync($"item?id={storyId}", cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Get all comment rows using XPath selectors
        var commentNodes = doc.DocumentNode.SelectNodes("//tr[contains(@class,'comtr')]");
        if (commentNodes == null || commentNodes.Count == 0) return comments;

        foreach (var commentNode in commentNodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var comment = ParseCommentFromNode(commentNode);
            if (comment != null) comments.Add(comment);
        }

        return comments;
    }

    private static WebComment? ParseCommentFromNode(HtmlNode commentNode)
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
            Debug.WriteLine($"Error parsing comment: {ex.Message}");
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

        // Fallback: try to parse the ISO date (first part)
        if (parts.Length >= 1 && DateTime.TryParse(parts[0], out var dateTime))
            return new DateTimeOffset(dateTime, TimeSpan.Zero).ToUnixTimeSeconds();

        return 0;
    }
}
