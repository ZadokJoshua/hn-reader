using System.Net;
using HtmlAgilityPack;

namespace HNReader.Core.Helpers;

public static class HtmlContentHelper
{
    private static readonly ReverseMarkdown.Converter MarkdownConverter = new(new ReverseMarkdown.Config
    {
        GithubFlavored = true,
        UnknownTags = ReverseMarkdown.Config.UnknownTagsOption.Bypass,
        SmartHrefHandling = true
    });

    public static string? ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return html;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var plainText = doc.DocumentNode.InnerText?.Trim() ?? string.Empty;
        return plainText;
    }

    public static string? ToMarkdown(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return html;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var innerHtml = doc.DocumentNode.InnerHtml ?? html;
        var markdown = MarkdownConverter.Convert(innerHtml);

        if (string.IsNullOrWhiteSpace(markdown))
        {
            return ToPlainText(html);
        }

        markdown = WebUtility.HtmlDecode(markdown);

        return markdown.Trim();
    }
}
