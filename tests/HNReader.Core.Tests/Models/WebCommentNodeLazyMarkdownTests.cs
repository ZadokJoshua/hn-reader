using HNReader.Core.Models;

namespace HNReader.Core.Tests.Models;

public class WebCommentNodeLazyMarkdownTests
{
    [Fact]
    public void MdText_IsNotComputedInConstructor()
    {
        // Build a comment whose HTML would be expensive to convert. We can't
        // easily assert "not called" without DI, but we can verify the access
        // works and is idempotent.
        var comment = new WebComment
        {
            Id = 1,
            By = "user1",
            Text = "<p>hello <a href='https://x.com'>link</a></p>",
            Depth = 0,
            Time = 0
        };

        var node = new WebCommentNode(comment);

        // First access computes the value.
        var first = node.MdText;
        Assert.NotNull(first);
        Assert.Contains("link", first!);

        // Second access returns the same instance (Lazy caches).
        var second = node.MdText;
        Assert.Same(first, second);
    }

    [Fact]
    public void MdText_NullCommentText_ReturnsNull()
    {
        var comment = new WebComment
        {
            Id = 1,
            By = "user1",
            Text = null,
            Depth = 0,
            Time = 0
        };

        var node = new WebCommentNode(comment);

        Assert.Null(node.MdText);
    }
}
