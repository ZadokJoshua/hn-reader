using HNReader.Core.Helpers;

namespace HNReader.Core.Tests.Helpers;

public class LRUCacheTests
{
    [Fact]
    public void Constructor_WithInvalidCapacity_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new LRUCache<int, string>(0));
        Assert.Throws<ArgumentException>(() => new LRUCache<int, string>(-1));
    }

    [Fact]
    public void Set_AndGet_WorksCorrectly()
    {
        // Arrange
        var cache = new LRUCache<int, string>(10);

        // Act
        cache.Set(1, "value1");
        var result = cache.TryGetValue(1, out var value);

        // Assert
        Assert.True(result);
        Assert.Equal("value1", value);
    }

    [Fact]
    public void TryGetValue_WithMissingKey_ReturnsFalse()
    {
        // Arrange
        var cache = new LRUCache<int, string>(10);

        // Act
        var result = cache.TryGetValue(999, out _);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Cache_EvictsLRUItem_WhenCapacityExceeded()
    {
        // Arrange
        var cache = new LRUCache<int, string>(3);
        cache.Set(1, "value1");
        cache.Set(2, "value2");
        cache.Set(3, "value3");

        // Act - Access 1 to mark as recently used
        _ = cache.TryGetValue(1, out _);

        // Add a 4th item - should evict 2 (least recently used)
        cache.Set(4, "value4");

        // Assert
        Assert.True(cache.TryGetValue(1, out _)); // Still there
        Assert.False(cache.TryGetValue(2, out _)); // Evicted
        Assert.True(cache.TryGetValue(3, out _)); // Still there
        Assert.True(cache.TryGetValue(4, out _)); // New item
    }

    [Fact]
    public void ContainsKey_ReturnsTrueForExistingKey()
    {
        // Arrange
        var cache = new LRUCache<int, string>(10);
        cache.Set(1, "value1");

        // Act
        var result = cache.ContainsKey(1);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsKey_ReturnsFalseForMissingKey()
    {
        // Arrange
        var cache = new LRUCache<int, string>(10);

        // Act
        var result = cache.ContainsKey(999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Remove_RemovesKeySuccessfully()
    {
        // Arrange
        var cache = new LRUCache<int, string>(10);
        cache.Set(1, "value1");

        // Act
        var result = cache.Remove(1);

        // Assert
        Assert.True(result);
        Assert.False(cache.TryGetValue(1, out _));
    }

    [Fact]
    public void Remove_WithNonexistentKey_ReturnsFalse()
    {
        // Arrange
        var cache = new LRUCache<int, string>(10);

        // Act
        var result = cache.Remove(999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        // Arrange
        var cache = new LRUCache<int, string>(10);
        cache.Set(1, "value1");
        cache.Set(2, "value2");
        cache.Set(3, "value3");

        // Act
        cache.Clear();

        // Assert
        Assert.Equal(0, cache.Count);
        Assert.False(cache.TryGetValue(1, out _));
    }

    [Fact]
    public void Count_ReturnsCorrectNumber()
    {
        // Arrange
        var cache = new LRUCache<int, string>(10);

        // Act & Assert
        Assert.Equal(0, cache.Count);

        cache.Set(1, "value1");
        Assert.Equal(1, cache.Count);

        cache.Set(2, "value2");
        Assert.Equal(2, cache.Count);

        cache.Remove(1);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void TTL_ExpiresItemsAfterTimeout()
    {
        // Arrange
        var cache = new LRUCache<int, string>(10, TimeSpan.FromMilliseconds(100));
        cache.Set(1, "value1");

        // Act - Immediate check
        var result1 = cache.TryGetValue(1, out var value1);
        Assert.True(result1);
        Assert.Equal("value1", value1);

        // Wait for expiration
        System.Threading.Thread.Sleep(150);

        // Act - Check after expiration
        var result2 = cache.TryGetValue(1, out _);

        // Assert
        Assert.False(result2);
    }

    [Fact]
    public void GetKeys_ReturnsAllValidKeys()
    {
        // Arrange
        var cache = new LRUCache<int, string>(10);
        cache.Set(1, "value1");
        cache.Set(2, "value2");
        cache.Set(3, "value3");

        // Act
        var keys = cache.GetKeys();

        // Assert
        Assert.Equal(3, keys.Count);
        Assert.Contains(1, keys);
        Assert.Contains(2, keys);
        Assert.Contains(3, keys);
    }

    [Fact]
    public void CleanupExpired_RemovesExpiredItems()
    {
        // Arrange
        var cache = new LRUCache<int, string>(10, TimeSpan.FromMilliseconds(100));
        cache.Set(1, "value1");

        System.Threading.Thread.Sleep(150); // Wait for expiration

        // Act
        var removedCount = cache.CleanupExpired();

        // Assert
        Assert.Equal(1, removedCount);
        Assert.False(cache.TryGetValue(1, out _));
    }

    [Fact]
    public void UpdateExistingKey_UpdatesValue()
    {
        // Arrange
        var cache = new LRUCache<int, string>(10);
        cache.Set(1, "original");

        // Act
        cache.Set(1, "updated");
        var result = cache.TryGetValue(1, out var value);

        // Assert
        Assert.True(result);
        Assert.Equal("updated", value);
        Assert.Equal(1, cache.Count); // Count should still be 1
    }

    [Fact]
    public void Cache_IsThreadSafe()
    {
        // Arrange
        var cache = new LRUCache<int, string>(100);
        var tasks = new List<Task>();

        // Act - Add items from multiple threads
        for (int t = 0; t < 10; t++)
        {
            var threadNum = t;
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < 50; i++)
                {
                    cache.Set(threadNum * 100 + i, $"value{i}");
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - All items should be there (some evicted due to capacity)
        Assert.True(cache.Count > 0);
        Assert.True(cache.Count <= 100); // Capacity limit
    }

    [Fact]
    public void AccessingItem_MarkesAsRecent()
    {
        // Arrange
        var cache = new LRUCache<int, string>(3);
        cache.Set(1, "value1");
        cache.Set(2, "value2");
        cache.Set(3, "value3");

        // Act - Access item 1 to make it recent
        _ = cache.TryGetValue(1, out _);

        // Add item 4 - should evict 2 (least recently used now)
        cache.Set(4, "value4");

        // Assert
        Assert.True(cache.TryGetValue(1, out _)); // Still exists
        Assert.False(cache.TryGetValue(2, out _)); // Evicted
    }
}
