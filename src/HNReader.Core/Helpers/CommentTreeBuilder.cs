using HNReader.Core.Models;

namespace HNReader.Core.Helpers;

/// <summary>
/// Helper class for building comment trees from flat lists.
/// Uses web-parsed comments with depth information.
/// </summary>
public static class CommentTreeBuilder
{
    /// <summary>
    /// Builds a tree of WebCommentNodes from a flat list of web-parsed comments.
    /// The comments already have depth information, making tree construction efficient.
    /// </summary>
    /// <param name="comments">Flat list of comments with depth info</param>
    /// <returns>Root-level comments with children populated</returns>
    public static List<WebCommentNode> BuildTree(List<WebComment> comments)
    {
        if (comments == null || comments.Count == 0) return [];

        var nodes = comments.Select(c => new WebCommentNode(c)).ToList();
        var rootNodes = new List<WebCommentNode>();
        var parentStack = new Stack<WebCommentNode>();

        foreach (var node in nodes)
        {
            // Pop from stack until we find the parent level
            while (parentStack.Count > 0 && parentStack.Peek().Depth >= node.Depth)
            {
                parentStack.Pop();
            }

            if (parentStack.Count == 0)
            {
                // This is a root-level comment
                rootNodes.Add(node);
            }
            else
            {
                // Add as child of the top of the stack
                parentStack.Peek().Children.Add(node);
            }

            // Push current node to potentially be a parent
            parentStack.Push(node);
        }

        return rootNodes;
    }
}
