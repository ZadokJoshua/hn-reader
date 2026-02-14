using HNReader.Core.Interfaces;
using HNReader.Core.Models;
using System.ComponentModel;
using System.Diagnostics;

namespace HNReader.Core.Services;

/// <summary>
/// Provides AI tool functions exposed to the Copilot CLI agent during digest generation.
/// Each method maps to a tool the agent can invoke.
/// </summary>
public class CopilotFunctions(
    IContentScraperService contentScraperService,
    HNWebClient hnWebClient)
{
    private const int MaxCommentsToReturn = 15;
    private const int MaxCommentLength = 500;
    private const int MaxTotalCommentChars = 4000;
    private const int MaxCommentDepth = 2;

    [Description("Fetch an external article URL and return its plain-text content")]
    public async Task<string> ScrapeArticleAsync(
        [Description("The full URL of the external article to scrape")] string url)
    {
        Debug.WriteLine($"[DigestGen] scrape_article called for {url}");
        var text = await contentScraperService.GetPlainTextAsync(url);
        return text;
    }

    [Description("Fetch comments for a Hacker News story")]
    public async Task<object> ReadCommentsAsync(
        [Description("The Hacker News story/object ID to fetch comments for")] int storyId)
    {
        Debug.WriteLine($"[DigestGen] read_comments called for story {storyId}");
        var comments = await hnWebClient.GetCommentsFromWebAsync(storyId);
        return ProcessCommentsForDigest(comments);
    }

    private static object ProcessCommentsForDigest(List<WebComment> comments)
    {
        var processedComments = new List<object>();
        var totalChars = 0;

        var eligibleComments = comments
            .Where(c => !string.IsNullOrWhiteSpace(c.Text) && c.Depth <= MaxCommentDepth)
            .ToList();

        foreach (var comment in eligibleComments)
        {
            if (processedComments.Count >= MaxCommentsToReturn)
                break;

            var text = comment.Text ?? string.Empty;

            if (text.Length > MaxCommentLength)
            {
                text = text[..MaxCommentLength] + "...";
            }

            if (totalChars + text.Length > MaxTotalCommentChars)
                break;

            processedComments.Add(new
            {
                author = comment.By,
                depth = comment.Depth,
                text
            });

            totalChars += text.Length;
        }

        Debug.WriteLine(
            $"[DigestGen] Returning {processedComments.Count} comments ({totalChars} chars) " +
            $"from {comments.Count} total (filtered {eligibleComments.Count} shallow)");

        return new
        {
            status = "ok",
            totalCommentCount = comments.Count,
            returnedCommentCount = processedComments.Count,
            comments = processedComments
        };
    }
}
