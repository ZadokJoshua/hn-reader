using HNReader.Core.Helpers;
using HNReader.Core.Models;

namespace HNReader.Core.Tests.Helpers;

public class CommentFlattenerTests
{
    private static int _nextId = 1;

    private static WebCommentNode Node(int depth, params WebCommentNode[] children)
    {
        var node = new WebCommentNode(new WebComment { Id = _nextId++, Depth = depth, Text = "x", By = "u" });
        foreach (var child in children) node.Children.Add(child);
        return node;
    }

    private static int[] Ids(IEnumerable<WebCommentNode> nodes) => nodes.Select(n => n.CommentId).ToArray();

    [Fact]
    public void Flatten_IsDepthFirstParentsBeforeChildren()
    {
        var grandchild = Node(2);
        var child = Node(1, grandchild);
        var root = Node(0, child);
        var second = Node(0);

        var flat = CommentFlattener.Flatten([root, second]);

        Assert.Equal(new[] { root.CommentId, child.CommentId, grandchild.CommentId, second.CommentId }, Ids(flat));
    }

    [Fact]
    public void Flatten_OmitsRepliesOfCollapsedNodes()
    {
        var child = Node(1);
        var root = Node(0, child);
        root.IsCollapsed = true;

        var flat = CommentFlattener.Flatten([root]);

        Assert.Equal(new[] { root.CommentId }, Ids(flat));
    }

    [Fact]
    public void CountSubtreeItemsAfter_CountsOnlyDeeperContiguousItems()
    {
        var grandchild = Node(2);
        var child = Node(1, grandchild);
        var root = Node(0, child);
        var second = Node(0);
        var flat = CommentFlattener.Flatten([root, second]);

        Assert.Equal(2, CommentFlattener.CountSubtreeItemsAfter(flat, 0));
        Assert.Equal(1, CommentFlattener.CountSubtreeItemsAfter(flat, 1));
        Assert.Equal(0, CommentFlattener.CountSubtreeItemsAfter(flat, 2));
        Assert.Equal(0, CommentFlattener.CountSubtreeItemsAfter(flat, 3));
    }

    [Fact]
    public void AppendVisibleDescendants_IgnoresTheNodesOwnCollapsedFlag()
    {
        var child = Node(1);
        var root = Node(0, child);
        root.IsCollapsed = true;

        var revealed = new List<WebCommentNode>();
        CommentFlattener.AppendVisibleDescendants(root, revealed);

        Assert.Equal(new[] { child.CommentId }, Ids(revealed));
    }

    [Fact]
    public void AppendVisibleDescendants_KeepsInnerCollapsedStateHidden()
    {
        var hidden = Node(2);
        var child = Node(1, hidden);
        child.IsCollapsed = true;
        var root = Node(0, child);

        var revealed = new List<WebCommentNode>();
        CommentFlattener.AppendVisibleDescendants(root, revealed);

        Assert.Equal(new[] { child.CommentId }, Ids(revealed));
    }

    [Fact]
    public void CollapseThenExpand_PatchedListMatchesFullFlatten()
    {
        var leafA = Node(2);
        var leafB = Node(2);
        var mid = Node(1, leafA, leafB);
        var root = Node(0, mid, Node(1));
        var other = Node(0, Node(1));
        var roots = new[] { root, other };

        var live = CommentFlattener.Flatten(roots);

        // Collapse `mid` and patch in place the way the UI does.
        mid.IsCollapsed = true;
        var index = live.IndexOf(mid);
        live.RemoveRange(index + 1, CommentFlattener.CountSubtreeItemsAfter(live, index));
        Assert.Equal(Ids(CommentFlattener.Flatten(roots)), Ids(live));

        // Expand it again.
        mid.IsCollapsed = false;
        var revealed = new List<WebCommentNode>();
        CommentFlattener.AppendVisibleDescendants(mid, revealed);
        live.InsertRange(index + 1, revealed);
        Assert.Equal(Ids(CommentFlattener.Flatten(roots)), Ids(live));
    }

    [Fact]
    public void LargeThread_FlattensQuickly()
    {
        var roots = Enumerable.Range(0, 200)
            .Select(_ => Node(0, Node(1, Node(2, Node(3)))))
            .ToList();

        var flat = CommentFlattener.Flatten(roots);

        Assert.Equal(800, flat.Count);
    }
}
