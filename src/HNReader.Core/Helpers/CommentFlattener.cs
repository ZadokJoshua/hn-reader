using HNReader.Core.Models;

namespace HNReader.Core.Helpers;

/// <summary>
/// Turns the comment tree into the flat, display-ordered list the comments pane renders,
/// and answers the two questions needed to patch that list in place when one comment is
/// collapsed or expanded.
/// </summary>
/// <remarks>
/// Lives in Core so it can be unit tested; the Avalonia control only owns the collection
/// that is bound to the UI. A node's visible descendants are always contiguous and directly
/// after it in the flat list, which is what makes a collapse a single range removal and an
/// expand a single range insertion rather than a rebuild of the whole thread.
/// </remarks>
public static class CommentFlattener
{
    /// <summary>
    /// Appends <paramref name="node"/> and every descendant that is visible. A collapsed node
    /// hides its replies, so they are left out entirely rather than added and made invisible:
    /// an invisible item still costs the panel a container and a measure.
    /// </summary>
    public static void AppendVisible(WebCommentNode node, ICollection<WebCommentNode> target)
    {
        target.Add(node);

        if (node.IsCollapsed) return;

        AppendVisibleDescendants(node, target);
    }

    /// <summary>
    /// Appends the descendants of <paramref name="node"/> that are visible when the node itself
    /// is expanded. The node's own <see cref="WebCommentNode.IsCollapsed"/> is deliberately not
    /// consulted, so this can be asked "what would expanding reveal" while it is still collapsed.
    /// Descendants that are themselves collapsed keep their own replies hidden.
    /// </summary>
    public static void AppendVisibleDescendants(WebCommentNode node, ICollection<WebCommentNode> target)
    {
        foreach (var child in node.Children)
        {
            AppendVisible(child, target);
        }
    }

    /// <summary>Flattens a whole forest of roots, honouring collapsed nodes.</summary>
    public static List<WebCommentNode> Flatten(IEnumerable<WebCommentNode> roots)
    {
        var result = new List<WebCommentNode>();
        foreach (var root in roots)
        {
            AppendVisible(root, result);
        }

        return result;
    }

    /// <summary>
    /// How many items directly after position <paramref name="index"/> in
    /// <paramref name="flat"/> belong to that node's subtree. A subtree is exactly the run of
    /// following items that are deeper than the node.
    /// </summary>
    public static int CountSubtreeItemsAfter(IReadOnlyList<WebCommentNode> flat, int index)
    {
        var depth = flat[index].Depth;
        var count = 0;

        while (index + 1 + count < flat.Count && flat[index + 1 + count].Depth > depth)
        {
            count++;
        }

        return count;
    }
}
