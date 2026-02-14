using HNReader.Core.Enums;
using HNReader.Core.Models;
using static HNReader.Core.Helpers.CoreHelper;

namespace HNReader.Core.Services;

public class HNClient(HttpClient httpClient)
{
    private Dictionary<StoryType, List<int>>? _storyIdsCache = [];

    /// <summary>
    /// Fetch a single Hacker News item by ID.
    /// </summary>
    /// <param name="itemType"></param>
    /// <param name="forceRefresh"></param>
    /// <returns></returns>
    private async Task<List<int>> GetStoryIdsAsync(StoryType itemType, bool forceRefresh = false)
    {
        if (!forceRefresh && _storyIdsCache != null && _storyIdsCache.TryGetValue(itemType, out var cachedIds))
            return cachedIds;

        var json = await httpClient.GetStringAsync(itemType.GetFeedEndpoint());
        var ids = Deserialize<List<int>>(json) ?? [];

        _storyIdsCache ??= new Dictionary<StoryType, List<int>>();
        _storyIdsCache[itemType] = ids;

        return ids;
    }

    /// <summary>
    /// Fetch single item by ID
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task<T?> GetItemAsync<T>(int id) where T : BaseHNItem
    {
        var json = await httpClient.GetStringAsync($"item/{id}.json");
        return Deserialize<T>(json);
    }

    /// <summary>
    /// Fetch a list of stories (default: topstories).
    /// </summary>
    /// <param name="itemType"></param>
    /// <param name="limit"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    public async Task<List<Story>> GetStoriesAsync(StoryType itemType = StoryType.Top, int limit = 20, int offset = 0)
    {
        var forceRefresh = offset == 0; // Refresh cache on first page
        var ids = await GetStoryIdsAsync(itemType, forceRefresh);
        var pagedIds = ids.Skip(offset).Take(limit);

        // Fetch concurrently
        var tasks = pagedIds.Select(GetItemAsync<Story>);
        var results = await Task.WhenAll(tasks);

        return [.. results.OfType<Story>()];
    }

    public void ClearCache() => _storyIdsCache?.Clear();

    private static long GetUnixTimestampSeconds24HoursAgo() => DateTimeOffset.UtcNow.AddHours(-24).ToUnixTimeSeconds();

    public async Task<HNSearchResult> GetStoriesFromLast24HoursAsync() 
    { 
        long since = GetUnixTimestampSeconds24HoursAgo();
        int totalHits = 50; // Max hits per page
        string url = $"https://hn.algolia.com/api/v1/search?tags=story&numericFilters=created_at_i>{since}&hitsPerPage={totalHits}";
        // Url with points filter example: $"https://hn.algolia.com/api/v1/search?tags=story&numericFilters=created_at_i>{since},points>100&hitsPerPage={totalHits}";
        var json = await httpClient.GetStringAsync(url);
        return Deserialize<HNSearchResult>(json) ?? new HNSearchResult();
    }
}
