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

        return NormalizeBullets(processed.Trim());
    }

    // A line that starts with one bullet character followed by whitespace and some text.
    private static readonly Regex BulletLineRegex = new(
        @"^[ \t]{0,3}(?<mark>[o\-+=\u2022\u00B7\u25E6\u2023])[ \t]+(?=\S)",
        RegexOptions.Compiled);

    /// <summary>
    /// HN has no list syntax, so people draw bullets by hand ("o item", "- item", a pasted
    /// bullet character), one per paragraph. Markdown only recognises some of those, and the
    /// renderer draws "-" as a hollow circle that reads as the letter O, so they are all
    /// rewritten to "*", the one marker that renders as a plain disc.
    /// </summary>
    /// <remarks>
    /// "-" and "+" are unambiguous list markers in markdown already, so they are always
    /// rewritten. The others ("o", "=" and bullet characters) are ordinary text on their own -
    /// a sentence can begin "o brother" - so they only count when two or more consecutive
    /// items use them, which is what a hand-drawn list looks like. Blank lines between items
    /// do not end a run; any other line does. Fenced code is left alone.
    /// </remarks>
    private static string NormalizeBullets(string markdown)
    {
        if (markdown.Length == 0) return markdown;

        var lines = markdown.Split('\n');
        var inFence = false;
        var run = new List<int>();

        void FlushRun()
        {
            // A lone weak marker is probably prose; a lone "-" or "+" is still a list item.
            var rewriteAll = run.Count >= 2;
            foreach (var i in run)
            {
                if (rewriteAll || lines[i].TrimStart()[0] is '-' or '+')
                {
                    lines[i] = Rewrite(lines[i]);
                }
            }

            run.Clear();
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                FlushRun();
                inFence = !inFence;
                continue;
            }

            if (inFence || string.IsNullOrWhiteSpace(line)) continue;

            if (BulletLineRegex.IsMatch(line))
            {
                run.Add(i);
            }
            else
            {
                FlushRun();
            }
        }

        FlushRun();

        return string.Join("\n", lines);
    }

    private static string Rewrite(string line) => BulletLineRegex.Replace(line, "* ", 1);
}
