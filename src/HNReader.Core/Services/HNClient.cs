using System.Collections.Concurrent;
using HNReader.Core.Enums;
using HNReader.Core.Models;
using static HNReader.Core.Helpers.CoreHelper;

namespace HNReader.Core.Services;

public class HNClient(HttpClient httpClient)
{
    private readonly ConcurrentDictionary<StoryType, StoryIdsCacheEntry> _storyIdsCache = [];
    private readonly TimeSpan _storyIdsCacheTtl = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _storyRequestSemaphore = new(initialCount: 5, maxCount: 5);

    private sealed record StoryIdsCacheEntry(List<int> Ids, DateTimeOffset CachedAtUtc);

    /// <summary>
    /// Fetch a single Hacker News item by ID.
    /// </summary>
    private async Task<List<int>> GetStoryIdsAsync(StoryType itemType, bool forceRefresh = false)
    {
        if (!forceRefresh && _storyIdsCache.TryGetValue(itemType, out var cached) && DateTimeOffset.UtcNow - cached.CachedAtUtc < _storyIdsCacheTtl)
        {
            return cached.Ids;
        }

        var json = await httpClient.GetStringAsync(itemType.GetFeedEndpoint()).ConfigureAwait(false);
        var ids = Deserialize<List<int>>(json) ?? [];

        _storyIdsCache[itemType] = new StoryIdsCacheEntry(ids, DateTimeOffset.UtcNow);

        return ids;
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
        var json = await httpClient.GetStringAsync($"item/{id}.json").ConfigureAwait(false);
        return Deserialize<T>(json);
    }

    /// <summary>
    /// Fetch a list of stories (default: topstories).
    /// </summary>
    public async Task<List<Story>> GetStoriesAsync(StoryType itemType = StoryType.Top, int limit = 20, int offset = 0)
    {
        var forceRefresh = offset == 0; // Refresh cache on first page
        var ids = await GetStoryIdsAsync(itemType, forceRefresh).ConfigureAwait(false);
        var pagedIds = ids.Skip(offset).Take(limit);

        // Fetch concurrently
        var tasks = pagedIds.Select(GetStoryWithLimitAsync);
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        return [.. results.OfType<Story>()];
    }

    public void ClearCache() => _storyIdsCache.Clear();
}
