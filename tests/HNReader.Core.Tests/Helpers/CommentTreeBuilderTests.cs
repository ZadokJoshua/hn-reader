using HNReader.Core.Helpers;
using HNReader.Core.Models;

namespace HNReader.Core.Tests.Helpers;

public class CommentTreeBuilderTests
{
    [Fact]
    public void BuildTree_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var comments = new List<WebComment>();

        // Act
        var result = CommentTreeBuilder.BuildTree(comments);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void BuildTree_WithNullList_ReturnsEmptyList()
    {
        // Act
        var result = CommentTreeBuilder.BuildTree(null!);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void BuildTree_WithFlatComments_BuildsHierarchyCorrectly()
    {
        // Arrange
        var comments = new List<WebComment>
        {
            new() { Id = 1, Depth = 0, By = "user1", Text = "Root 1" },
            new() { Id = 2, Depth = 1, By = "user2", Text = "Child of 1" },
            new() { Id = 3, Depth = 2, By = "user3", Text = "Grandchild of 1" },
            new() { Id = 4, Depth = 1, By = "user4", Text = "Another child of 1" },
            new() { Id = 5, Depth = 0, By = "user5", Text = "Root 2" },
        };

        // Act
        var result = CommentTreeBuilder.BuildTree(comments);

        // Assert
        Assert.Equal(2, result.Count); // Two root nodes
        Assert.Equal(1, result[0].CommentId);
        Assert.Equal(5, result[1].CommentId);

        // Verify first root has correct children
        Assert.Equal(2, result[0].Children.Count);
        Assert.Equal(2, result[0].Children[0].CommentId);
        Assert.Equal(4, result[0].Children[1].CommentId);

        // Verify nested hierarchy
        Assert.Single(result[0].Children[0].Children);
        Assert.Equal(3, result[0].Children[0].Children[0].CommentId);
    }

    [Fact]
    public void BuildTree_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var comments = Enumerable.Range(0, 100)
            .Select(i => new WebComment { Id = i, Depth = 0, By = $"user{i}", Text = $"Comment {i}" })
            .ToList();

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        Assert.Throws<OperationCanceledException>(() => 
            CommentTreeBuilder.BuildTree(comments, cts.Token));
    }

    [Fact]
    public void BuildTree_WithDeepNesting_BuildsCorrectly()
    {
        // Arrange: Create a deeply nested thread (common in HN discussions)
        var comments = new List<WebComment>();
        for (int i = 0; i < 10; i++)
        {
            comments.Add(new WebComment { Id = i, Depth = i, By = $"user{i}", Text = $"Level {i}" });
        }

        // Act
        var result = CommentTreeBuilder.BuildTree(comments);

        // Assert
        Assert.Single(result); // Only one root
        Assert.Equal(0, result[0].CommentId);

        // Verify chain of nesting
        var current = result[0];
        for (int i = 1; i < 10; i++)
        {
            Assert.Single(current.Children);
            current = current.Children[0];
            Assert.Equal(i, current.CommentId);
        }
    }

    [Fact]
    public void BuildTree_PerformanceTest_1000CommentsLessThan100ms()
    {
        // Arrange
        var comments = Enumerable.Range(0, 1000)
            .Select(i => new WebComment 
            { 
                Id = i, 
                Depth = i % 5, // Cycle through depth 0-4
                By = $"user{i}", 
                Text = $"Comment {i}" 
            })
            .ToList();

        var cts = new CancellationTokenSource();

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = CommentTreeBuilder.BuildTree(comments, cts.Token);
        sw.Stop();

        // Assert
        Assert.NotEmpty(result);
        Assert.True(sw.ElapsedMilliseconds < 100, 
            $"Tree building took {sw.ElapsedMilliseconds}ms, expected < 100ms");
    }

    [Fact]
    public void BuildTree_WithMultipleBranches_MaintainsStructure()
    {
        // Arrange
        var comments = new List<WebComment>
        {
            new() { Id = 1, Depth = 0, By = "user1", Text = "Root" },
            new() { Id = 2, Depth = 1, By = "user2", Text = "Branch A" },
            new() { Id = 3, Depth = 2, By = "user3", Text = "Sub-branch A1" },
            new() { Id = 4, Depth = 1, By = "user4", Text = "Branch B" },
            new() { Id = 5, Depth = 2, By = "user5", Text = "Sub-branch B1" },
            new() { Id = 6, Depth = 2, By = "user6", Text = "Sub-branch B2" },
        };

        // Act
        var result = CommentTreeBuilder.BuildTree(comments);

        // Assert
        Assert.Single(result);
        Assert.Equal(2, result[0].Children.Count);

        // Branch A
        Assert.Single(result[0].Children[0].Children);
        // Branch B
        Assert.Equal(2, result[0].Children[1].Children.Count);
    }
}
