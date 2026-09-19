using System.Text.Json;
using HNReader.Server.Configuration;
using HNReader.Server.Models;
using Microsoft.Extensions.Options;

namespace HNReader.Server.Services;

/// <summary>
/// Sources candidate stories from HN's Algolia search API instead of the
/// Firebase per-story-type feeds — targets genuine recent popularity (points,
/// comment activity) in one call rather than fixed quotas split across
/// Top/New/Best/Show/Ask.
/// </summary>
public class AlgoliaSearchService
{
    private readonly HttpClient _httpClient;
    private readonly DigestOptions _options;
    private readonly ILogger<AlgoliaSearchService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AlgoliaSearchService(HttpClient httpClient, IOptions<DigestOptions> options, ILogger<AlgoliaSearchService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<HNSearchHit>> GetPopularStoriesAsync(CancellationToken cancellationToken = default)
    {
        var since = DateTimeOffset.UtcNow.AddHours(-_options.AlgoliaLookbackHours).ToUnixTimeSeconds();
        var numericFilters = $"created_at_i>{since}";
        if (_options.MinPoints > 0)
        {
            numericFilters += $",points>{_options.MinPoints}";
        }

        var url = $"search?tags=story&numericFilters={Uri.EscapeDataString(numericFilters)}&hitsPerPage={_options.MaxCandidatePoolSize}";

        // Deliberately does NOT swallow failures into an empty list: with no
        // candidates the whole run produces an empty digest that would then be
        // persisted as "today's digest", replacing a good one with nothing.
        // Throwing lets Hangfire retry and leaves the last good digest served.
        try
        {
            var json = await _httpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<HNSearchResult>(json, JsonOptions);
            return result?.Hits ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Digest: Algolia candidate search failed");
            throw new InvalidOperationException("Algolia candidate search failed; aborting digest run.", ex);
        }
    }
}
