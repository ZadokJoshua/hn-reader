using HNReader.Core.Models;
using System.Text;

namespace HNReader.Core.Helpers;

public static class MarkdownGenerator
{
    public static string BuildStoryMarkdown(StoryData storyData, List<WebCommentNode> rootNodes)
    {
        var markdownBuilder = new StringBuilder();

        markdownBuilder.AppendLine($"# {storyData.Title}");
        markdownBuilder.AppendLine($"ID:{storyData.Id}|By:{storyData.By}");
        markdownBuilder.AppendLine();

        markdownBuilder.AppendLine("## Content");
        if (!string.IsNullOrWhiteSpace(storyData.Content))
        {
            markdownBuilder.AppendLine($"{storyData.Content.Trim()}");
        }
        else
        {
            markdownBuilder.Append("*NO CONTENT FOR THIS STORY*");
        }
        markdownBuilder.AppendLine();

        markdownBuilder.AppendLine("---");
        markdownBuilder.AppendLine("## Discussion");

        foreach (var root in rootNodes) AppendNodeRecursive(root, markdownBuilder);

        return markdownBuilder.ToString();
    }

    private static void AppendNodeRecursive(WebCommentNode node, StringBuilder markdownBuilder)
    {
        var content = HtmlContentHelper.ToPlainText(node.Comment?.Text);
        if (string.IsNullOrWhiteSpace(content)) return;

        var normalizedContent = NormalizeCommentContent(content);
        var depthPrefix = node.Depth <= 0
            ? string.Empty
            : $"{new string(' ', node.Depth * 2)}↳ ";

        markdownBuilder.AppendLine($"{depthPrefix}@{node.By}: {normalizedContent}");

        // Recursively visit children (Depth-First)
        foreach (var child in node.Children) AppendNodeRecursive(child, markdownBuilder);
    }

    private static string NormalizeCommentContent(string content)
    {
        var lines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        return lines.Length == 0 ? string.Empty : string.Join(" | ", lines);
    }
}

public record StoryData(int Id, string Title, string By, string Content);