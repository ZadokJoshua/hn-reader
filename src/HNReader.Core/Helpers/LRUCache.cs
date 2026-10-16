using System.Collections.Generic;
using System.Linq;

namespace HNReader.Core.Helpers;

/// <summary>
/// A thread-safe generic LRU (Least Recently Used) cache with optional TTL (Time-To-Live) support.
/// When the cache reaches maximum capacity, the least recently accessed item is evicted.
/// Items can optionally expire after a specified TTL has passed.
/// </summary>
/// <typeparam name="TKey">The type of cache keys</typeparam>
/// <typeparam name="TValue">The type of cached values</typeparam>
public class LRUCache<TKey, TValue> where TKey : notnull
{
    private readonly int _maxCapacity;
    private readonly TimeSpan? _defaultTtl;
    private readonly Dictionary<TKey, CacheEntry> _cache;
    private readonly LinkedList<TKey> _accessOrder; // Tracks LRU order: most recent at end
    private readonly object _lockObj = new object();

    /// <summary>
    /// Initializes a new LRU cache with the specified maximum capacity.
    /// </summary>
    /// <param name="maxCapacity">Maximum number of items to store in cache</param>
    /// <param name="defaultTtl">Optional default time-to-live for cached items. null = no expiration</param>
    /// <exception cref="ArgumentException">Thrown if maxCapacity is less than 1</exception>
    public LRUCache(int maxCapacity, TimeSpan? defaultTtl = null)
    {
        if (maxCapacity < 1)
            throw new ArgumentException("maxCapacity must be at least 1", nameof(maxCapacity));

        _maxCapacity = maxCapacity;
        _defaultTtl = defaultTtl;
        _cache = new Dictionary<TKey, CacheEntry>(maxCapacity);
        _accessOrder = new LinkedList<TKey>();
    }

    /// <summary>
    /// Gets the number of items currently in the cache.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lockObj)
            {
                return _cache.Count;
            }
        }
    }

    /// <summary>
    /// Attempts to get a value from the cache.
    /// </summary>
    /// <param name="key">The key to look up</param>
    /// <param name="value">The cached value, if found and not expired</param>
    /// <returns>true if the key was found and value is not expired; false otherwise</returns>
    public bool TryGetValue(TKey key, out TValue? value)
    {
        lock (_lockObj)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                // Check if the entry has expired
                if (entry.IsExpired)
                {
                    _cache.Remove(key);
                    _accessOrder.Remove(entry.AccessNode);
                    value = default;
                    return false;
                }

                // Mark as recently accessed by moving to end
                _accessOrder.Remove(entry.AccessNode);
                entry.AccessNode = _accessOrder.AddLast(key);

                value = entry.Value;
                return true;
            }

            value = default;
            return false;
        }
    }

    /// <summary>
    /// Adds or updates a value in the cache.
    /// If the cache is at capacity, the least recently used item is evicted.
    /// </summary>
    /// <param name="key">The cache key</param>
    /// <param name="value">The value to cache</param>
    public void Set(TKey key, TValue value)
    {
        lock (_lockObj)
        {
            if (_cache.TryGetValue(key, out var existing))
            {
                // Update existing entry
                existing.Value = value;
                existing.CreatedAt = DateTime.UtcNow;

                // Mark as recently accessed
                _accessOrder.Remove(existing.AccessNode);
                existing.AccessNode = _accessOrder.AddLast(key);
            }
            else
            {
                // Add new entry
                if (_cache.Count >= _maxCapacity)
                {
                    // Evict least recently used (first in access order)
                    var lruKey = _accessOrder.First!.Value;
                    _accessOrder.RemoveFirst();
                    _cache.Remove(lruKey);
                }

                var accessNode = _accessOrder.AddLast(key);
                _cache[key] = new CacheEntry(_defaultTtl)
                {
                    Value = value,
                    CreatedAt = DateTime.UtcNow,
                    AccessNode = accessNode
                };
            }
        }
    }

    /// <summary>
    /// Checks if a key exists in the cache and has not expired.
    /// Does not mark the item as accessed.
    /// </summary>
    /// <param name="key">The key to check</param>
    /// <returns>true if the key exists and is not expired; false otherwise</returns>
    public bool ContainsKey(TKey key)
    {
        lock (_lockObj)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.IsExpired)
                {
                    _cache.Remove(key);
                    _accessOrder.Remove(entry.AccessNode);
                    return false;
                }
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Removes a specific key from the cache.
    /// </summary>
    /// <param name="key">The key to remove</param>
    /// <returns>true if the key was found and removed; false otherwise</returns>
    public bool Remove(TKey key)
    {
        lock (_lockObj)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                _accessOrder.Remove(entry.AccessNode);
                _cache.Remove(key);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Clears all items from the cache.
    /// </summary>
    public void Clear()
    {
        lock (_lockObj)
        {
            _cache.Clear();
            _accessOrder.Clear();
        }
    }

    /// <summary>
    /// Removes all expired entries from the cache.
    /// Useful to call periodically to free memory from stale entries.
    /// </summary>
    /// <returns>The number of entries removed</returns>
    public int CleanupExpired()
    {
        lock (_lockObj)
        {
            var expiredKeys = _cache
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                if (_cache.TryGetValue(key, out var entry))
                {
                    _accessOrder.Remove(entry.AccessNode);
                    _cache.Remove(key);
                }
            }

            return expiredKeys.Count;
        }
    }

    /// <summary>
    /// Gets all keys currently in the cache (excluding expired entries).
    /// </summary>
    /// <returns>List of valid cache keys</returns>
    public List<TKey> GetKeys()
    {
        lock (_lockObj)
        {
            return _cache
                .Where(kvp => !kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();
        }
    }

    private class CacheEntry
    {
        private readonly TimeSpan? _entryTtl;

        public TValue? Value { get; set; }
        public DateTime CreatedAt { get; set; }
        public LinkedListNode<TKey>? AccessNode { get; set; }

        public CacheEntry(TimeSpan? ttl)
        {
            _entryTtl = ttl;
        }

        /// <summary>
        /// Determines if this entry has expired based on the configured TTL.
        /// </summary>
        public bool IsExpired
        {
            get
            {
                // If no TTL is configured, entries never expire
                if (_entryTtl == null)
                    return false;

                var age = DateTime.UtcNow - CreatedAt;
                return age > _entryTtl.Value;
            }
        }
    }
}
