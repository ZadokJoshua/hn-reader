using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace HNReader.Core.Helpers;

public static class HtmlContentHelper
{
    // Full ReverseMarkdown converter kept as fallback for complex/non-comment HTML
    private static readonly ReverseMarkdown.Converter MarkdownConverter = new(new ReverseMarkdown.Config
    {
        GithubFlavored = true,
        UnknownTags = ReverseMarkdown.Config.UnknownTagsOption.Bypass,
        SmartHrefHandling = true
    });

    // Precompiled regex for extracting <a> tags — used by the fast path.
    // HN comments only contain simple <a href="...">text</a> links.
    private static readonly Regex AnchorTagRegex = new(
        @"<a\s[^>]*href\s*=\s*""([^""]*)""\s*[^>]*>(.*?)</a>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    public static string? ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return html;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var plainText = doc.DocumentNode.InnerText?.Trim() ?? string.Empty;
        return plainText;
    }

    /// <summary>
    /// Converts HN comment HTML to Markdown using a fast string-replacement approach.
    /// Inspired by the EmergeTools/hackernews app rendering pipeline:
    ///   1. Extract links via regex → markdown link syntax
    ///   2. Decode HTML entities
    ///   3. Convert remaining HTML tags to markdown equivalents
    /// This avoids full DOM parsing + ReverseMarkdown for every comment,
    /// providing significantly faster conversion for typical HN comment HTML.
    /// </summary>
    public static string? ToMarkdownFast(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return html;

        var processed = html;

        // Phase 1: Convert <a href="...">text</a> → [text](href)
        processed = AnchorTagRegex.Replace(processed, match =>
        {
            var href = match.Groups[1].Value;
            var text = match.Groups[2].Value.Trim();
            // If the display text IS the URL, just use the URL as a bare link
            if (string.Equals(text, href, StringComparison.OrdinalIgnoreCase))
                return $"[{text}]({href})";
            return $"[{text}]({href})";
        });

        // Phase 2: Convert HTML tags to Markdown equivalents.
        // Order matters: process block-level tags before inline.
        processed = processed
            .Replace("<pre><code>", "\n```\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</code></pre>", "\n```\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<code>", "`", StringComparison.OrdinalIgnoreCase)
            .Replace("</code>", "`", StringComparison.OrdinalIgnoreCase)
            .Replace("<p>", "\n\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</p>", "", StringComparison.OrdinalIgnoreCase)
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<i>", "*", StringComparison.OrdinalIgnoreCase)
            .Replace("</i>", "*", StringComparison.OrdinalIgnoreCase)
            .Replace("<em>", "*", StringComparison.OrdinalIgnoreCase)
            .Replace("</em>", "*", StringComparison.OrdinalIgnoreCase)
            .Replace("<b>", "**", StringComparison.OrdinalIgnoreCase)
            .Replace("</b>", "**", StringComparison.OrdinalIgnoreCase)
            .Replace("<strong>", "**", StringComparison.OrdinalIgnoreCase)
            .Replace("</strong>", "**", StringComparison.OrdinalIgnoreCase);

        // Phase 3: Decode HTML entities AFTER tag conversion
        // (must happen after so entities inside markdown syntax are correct)
        processed = WebUtility.HtmlDecode(processed);

        return processed.Trim();
    }

    /// <summary>
    /// Full-fidelity HTML-to-Markdown conversion using ReverseMarkdown.
    /// Use for story body text or complex HTML. For HN comments, prefer ToMarkdownFast().
    /// </summary>
    public static string? ToMarkdown(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return html;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var innerHtml = doc.DocumentNode.InnerHtml ?? html;
        var markdown = MarkdownConverter.Convert(innerHtml);

        if (string.IsNullOrWhiteSpace(markdown)) return ToPlainText(html);

        markdown = WebUtility.HtmlDecode(markdown);

        return markdown.Trim();
    }
}
