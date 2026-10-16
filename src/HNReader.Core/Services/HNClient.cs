using System.Collections.Concurrent;
using System.Diagnostics;
using HNReader.Core.Enums;
using HNReader.Core.Models;
using HNReader.Core.Services.Logging;
using static HNReader.Core.Helpers.CoreHelper;

namespace HNReader.Core.Services;

public class HNClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<StoryType, StoryIdsCacheEntry> _storyIdsCache = [];
    private readonly TimeSpan _storyIdsCacheTtl = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _storyRequestSemaphore = new(initialCount: 5, maxCount: 5);

    private sealed record StoryIdsCacheEntry(List<int> Ids, DateTimeOffset CachedAtUtc);

    public HNClient(HttpClient httpClient, ILogger? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Fetch a single Hacker News item by ID.
    /// </summary>
    private async Task<List<int>> GetStoryIdsAsync(StoryType itemType, bool forceRefresh = false)
    {
        if (!forceRefresh && _storyIdsCache.TryGetValue(itemType, out var cached) && DateTimeOffset.UtcNow - cached.CachedAtUtc < _storyIdsCacheTtl)
        {
            return cached.Ids;
        }

        var endpoint = itemType.GetFeedEndpoint();
        var sw = Stopwatch.StartNew();
        try
        {
            var json = await _httpClient.GetStringAsync(endpoint).ConfigureAwait(false);
            sw.Stop();
            var ids = Deserialize<List<int>>(json) ?? [];
            _storyIdsCache[itemType] = new StoryIdsCacheEntry(ids, DateTimeOffset.UtcNow);
            _logger?.LogInformation("HN", "feed fetched",
                context: ContextOf(("endpoint", endpoint), ("count", ids.Count), ("durationMs", sw.ElapsedMilliseconds)));
            return ids;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger?.LogError("HN", "feed fetch failed", ex,
                context: ContextOf(("endpoint", endpoint), ("durationMs", sw.ElapsedMilliseconds)));
            throw;
        }
    }

    private async Task<Story?> GetStoryWithLimitAsync(int id)
    {
        await _storyRequestSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            return await GetItemAsync<Story>(id).ConfigureAwait(false);
        }
        finally
        {
            _storyRequestSemaphore.Release();
        }
    }

    /// <summary>
    /// Fetch single item by ID
    /// </summary>
    public async Task<T?> GetItemAsync<T>(int id) where T : BaseHNItem
    {
        var endpoint = $"item/{id}.json";
        var sw = Stopwatch.StartNew();
        try
        {
            var json = await _httpClient.GetStringAsync(endpoint).ConfigureAwait(false);
            sw.Stop();
            var item = Deserialize<T>(json);
            _logger?.LogDebug("HN", "item fetched",
                context: ContextOf(("endpoint", endpoint), ("durationMs", sw.ElapsedMilliseconds)));
            return item;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger?.LogWarning("HN", "item fetch failed", ex,
                context: ContextOf(("endpoint", endpoint), ("durationMs", sw.ElapsedMilliseconds)));
            throw;
        }
    }

    /// <summary>
    /// Fetch a list of stories (default: topstories).
    /// </summary>
    public async Task<List<Story>> GetStoriesAsync(StoryType itemType = StoryType.Top, int limit = 20, int offset = 0)
    {
        var (stories, _) = await GetStoriesPageAsync(itemType, limit, offset).ConfigureAwait(false);
        return stories;
    }

    /// <summary>
    /// Fetch a list of stories, also returning the raw ID-list cursor the next
    /// page should resume from. The HN feed frequently includes a few
    /// dead/deleted IDs that the item API silently returns null for (no error,
    /// no exception) — those are skipped, and the ID window keeps expanding
    /// until either <paramref name="limit"/> valid stories are collected or the
    /// feed's ID list is exhausted. This guarantees a full page whenever more
    /// data actually exists, so callers can reliably treat "fewer than limit
    /// stories returned" as "no more pages" without under-counting due to a
    /// handful of unrelated dead IDs.
    /// </summary>
    public async Task<(List<Story> Stories, int NextOffset)> GetStoriesPageAsync(StoryType itemType = StoryType.Top, int limit = 20, int offset = 0)
    {
        var forceRefresh = offset == 0; // Refresh cache on first page
        var ids = await GetStoryIdsAsync(itemType, forceRefresh).ConfigureAwait(false);

        var results = new List<Story>(limit);
        var cursor = offset;

        while (results.Count < limit && cursor < ids.Count)
        {
            var needed = limit - results.Count;
            var batchIds = ids.GetRange(cursor, Math.Min(needed, ids.Count - cursor));

            var tasks = batchIds.Select(GetStoryWithLimitAsync);
            var batchResults = await Task.WhenAll(tasks).ConfigureAwait(false);
            results.AddRange(batchResults.OfType<Story>());

            cursor += batchIds.Count;
        }

        return (results, cursor);
    }

    public void ClearCache() => _storyIdsCache.Clear();

    private static IReadOnlyList<KeyValuePair<string, object?>> ContextOf(
        params (string Key, object? Value)[] pairs)
    {
        var list = new List<KeyValuePair<string, object?>>(pairs.Length);
        foreach (var (k, v) in pairs) list.Add(new KeyValuePair<string, object?>(k, v));
        return list;
    }
}
