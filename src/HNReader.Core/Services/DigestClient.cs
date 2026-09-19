using System.Diagnostics;
using System.Net;
using HNReader.Core.Constants;
using HNReader.Core.Models;
using HNReader.Core.Services.Logging;
using HNReader.Shared.Models;
using static HNReader.Core.Helpers.CoreHelper;

namespace HNReader.Core.Services;

/// <summary>
/// Reads the daily digest and its category taxonomy from our own
/// HNReader.Server. The only client in this app that talks to a server we run
/// rather than to Hacker News directly.
///
/// <para>
/// Deliberately stateless — no response caching here. The digest is cached by
/// the ViewModel, which is the singleton; a cache on this type would be thrown
/// away on every navigation, since it is registered transient like
/// <see cref="HNClient"/> and <see cref="HNWebClient"/>.
/// </para>
/// </summary>
public class DigestClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger? _logger;

    public DigestClient(HttpClient httpClient, ILogger? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Fetches the latest digest, optionally filtered to specific categories.
    /// </summary>
    /// <param name="categories">
    /// Category names to include, or null/empty for the whole digest. Unknown
    /// names are silently dropped by the server rather than erroring.
    /// </param>
    public async Task<DigestResult> GetDigestAsync(
        IReadOnlyCollection<string>? categories = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = BuildDigestEndpoint(categories);
        var sw = Stopwatch.StartNew();

        try
        {
            using var response = await _httpClient
                .GetAsync(endpoint, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // Documented, expected state: no nightly run has completed yet.
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var envelope = TryReadEnvelope<DigestDto>(body);

                _logger?.LogInformation("Digest", "no digest available yet", context: ContextOf(
                    ("durationMs", sw.ElapsedMilliseconds)));
                return DigestResult.NotGenerated(envelope?.Error);
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger?.LogWarning("Digest", "digest request was rate limited");
                return DigestResult.RateLimited();
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var result = DeserializeWeb<OperationResponse<DigestDto>>(json);

            if (result is null || !result.Success || result.Data is null)
            {
                // A 200 whose envelope says otherwise. Reported as "nothing to
                // show" rather than fabricated into an empty digest, so the UI
                // can surface the server's own message.
                _logger?.LogWarning("Digest", "digest response carried no data", context: ContextOf(
                    ("success", result?.Success), ("error", result?.Error)));
                return DigestResult.NotGenerated(result?.Error);
            }

            _logger?.LogInformation("Digest", "digest fetched", context: ContextOf(
                ("categories", result.Data.Categories.Count()),
                ("durationMs", sw.ElapsedMilliseconds)));

            return DigestResult.Ok(result.Data);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError("Digest", "digest fetch failed", ex, context: ContextOf(
                ("endpoint", endpoint), ("durationMs", sw.ElapsedMilliseconds)));
            throw;
        }
    }

    /// <summary>
    /// Fetches the category taxonomy, in the server's display order.
    /// <para>
    /// Never throws: a taxonomy failure returns an empty list. The taxonomy only
    /// affects how the digest is ordered and labelled, so losing it must not sink
    /// a digest that fetched perfectly well.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var json = await _httpClient
                .GetStringAsync(AppFileNames.DIGEST_CATEGORIES_ENDPOINT, cancellationToken)
                .ConfigureAwait(false);

            var result = DeserializeWeb<OperationResponse<List<DigestTaxonomyCategoryDto>>>(json);
            var names = result?.Data?.Select(c => c.Name).ToList() ?? [];

            _logger?.LogInformation("Digest", "categories fetched", context: ContextOf(
                ("count", names.Count), ("durationMs", sw.ElapsedMilliseconds)));

            return names;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("Digest", "categories fetch failed; falling back to digest order", ex);
            return [];
        }
    }

    /// <summary>
    /// Asks the server to make sure today's digest exists, and reports what it
    /// did. Cheap and safe to call: the server starts a run only when there is
    /// genuinely no digest for the current UTC day and none already running, so
    /// this cannot trigger more work than the nightly job already does.
    /// </summary>
    public async Task<DigestGenerationStatusDto?> RequestGenerationAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient
                .PostAsync(AppFileNames.DIGEST_GENERATE_ENDPOINT, content: null, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Digest", "generation request refused", context: ContextOf(
                    ("status", (int)response.StatusCode)));
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var result = DeserializeWeb<OperationResponse<DigestGenerationStatusDto>>(json);

            _logger?.LogInformation("Digest", "generation requested", context: ContextOf(
                ("outcome", result?.Data?.Status)));

            return result?.Data;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Best-effort: failing to *ask* for a digest must not break showing
            // the one we already have.
            _logger?.LogWarning("Digest", "generation request failed", ex);
            return null;
        }
    }

    private static string BuildDigestEndpoint(IReadOnlyCollection<string>? categories)
    {
        if (categories is null || categories.Count == 0)
        {
            return AppFileNames.DIGEST_ENDPOINT;
        }

        // Each name is escaped individually before joining. Category names
        // routinely contain spaces and ampersands ("Startups, Funding &
        // Business"); left raw, the '&' would start a new query parameter and
        // silently truncate the filter.
        var escaped = string.Join(',', categories.Select(Uri.EscapeDataString));
        return $"{AppFileNames.DIGEST_ENDPOINT}?categories={escaped}";
    }

    private OperationResponse<T>? TryReadEnvelope<T>(string body)
    {
        try
        {
            return DeserializeWeb<OperationResponse<T>>(body);
        }
        catch (Exception ex)
        {
            // An unreadable error body is not worth failing over — the caller
            // already knows the outcome from the status code.
            _logger?.LogDebug("Digest", "could not parse error envelope", ex, context: null);
            return null;
        }
    }

    private static IReadOnlyList<KeyValuePair<string, object?>> ContextOf(
        params (string Key, object? Value)[] pairs)
    {
        var list = new List<KeyValuePair<string, object?>>(pairs.Length);
        foreach (var (k, v) in pairs) list.Add(new KeyValuePair<string, object?>(k, v));
        return list;
    }
}
