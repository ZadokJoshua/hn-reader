using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace HNReader.Core.Helpers;

public static class HtmlContentHelper
{
    // Precompiled regex for extracting <a> tags — used by the fast path.
    // HN comments only contain simple <a href="...">text</a> links.
    private static readonly Regex AnchorTagRegex = new(
        @"<a\s[^>]*href\s*=\s*(?:\""(?<href1>[^\""#<>\s][^\""<>]*)\""|'(?<href2>[^'#<>\s][^'<>]*)'|(?<href3>[^\s>]+))[^>]*>(?<text>.*?)</a>",
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
    ///   1. Extract links via regex → markdown link syntax
    ///   2. Decode HTML entities
    ///   3. Convert remaining HTML tags to markdown equivalents
    /// </summary>
    public static string? ToMarkdown(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return html;

        var processed = html;

        // Phase 1: Convert <a href="...">text</a> → [text](href)
        processed = AnchorTagRegex.Replace(processed, match =>
        {
            var href = match.Groups["href1"].Value;
            if (string.IsNullOrWhiteSpace(href)) href = match.Groups["href2"].Value;
            if (string.IsNullOrWhiteSpace(href)) href = match.Groups["href3"].Value;

            if (string.IsNullOrWhiteSpace(href))
            {
                return match.Groups["text"].Value;
            }

            var text = match.Groups["text"].Value.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                text = href;
            }

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
}
