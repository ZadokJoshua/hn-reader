using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;

namespace HNReader.Core.Services;

/// <summary>
/// Coordinates public HN HTML requests across the app's transient HNWebClient instances.
/// </summary>
public sealed class HNWebRequestCoordinator
{
    private static readonly TimeSpan MaximumBackoff = TimeSpan.FromMinutes(1);
    private const int BaseBackoffSeconds = 2;

    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private readonly ConcurrentDictionary<int, Lazy<Task<string>>> _inFlight = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _minimumRequestInterval;
    private DateTimeOffset _lastRequestStartedUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _backoffUntilUtc = DateTimeOffset.MinValue;
    private int _consecutiveTransientFailures;

    public HNWebRequestCoordinator(
        TimeProvider? timeProvider = null,
        TimeSpan? minimumRequestInterval = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _minimumRequestInterval = minimumRequestInterval ?? TimeSpan.FromSeconds(1);
        if (_minimumRequestInterval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(minimumRequestInterval));
    }

    /// <summary>
    /// Fetches one public item page. Concurrent callers for the same story share its
    /// request; different stories are serialized and spaced to avoid bursty HTML traffic.
    /// Caller cancellation stops that caller waiting without cancelling a request shared
    /// with other views.
    /// </summary>
    public Task<string> GetItemHtmlAsync(HttpClient httpClient, int storyId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        if (storyId <= 0) throw new ArgumentOutOfRangeException(nameof(storyId));

        var request = _inFlight.GetOrAdd(storyId, id => new Lazy<Task<string>>(
            () => FetchAndRemoveAsync(httpClient, id),
            LazyThreadSafetyMode.ExecutionAndPublication));

        return request.Value.WaitAsync(cancellationToken);
    }

    private async Task<string> FetchAndRemoveAsync(HttpClient httpClient, int storyId)
    {
        try
        {
            return await FetchAsync(httpClient, storyId).ConfigureAwait(false);
        }
        finally
        {
            _inFlight.TryRemove(storyId, out _);
        }
    }

    private async Task<string> FetchAsync(HttpClient httpClient, int storyId)
    {
        await _requestGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await WaitUntilAllowedAsync().ConfigureAwait(false);
            _lastRequestStartedUtc = _timeProvider.GetUtcNow();

            using var response = await httpClient.GetAsync(
                $"item?id={storyId}",
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
            {
                SetBackoff(response.Headers.RetryAfter);
            }

            response.EnsureSuccessStatusCode();
            _consecutiveTransientFailures = 0;
            _backoffUntilUtc = DateTimeOffset.MinValue;
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    private async Task WaitUntilAllowedAsync()
    {
        var now = _timeProvider.GetUtcNow();
        var nextBySpacing = _lastRequestStartedUtc + _minimumRequestInterval;
        var waitUntil = nextBySpacing > _backoffUntilUtc ? nextBySpacing : _backoffUntilUtc;
        var delay = waitUntil - now;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, _timeProvider, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private void SetBackoff(RetryConditionHeaderValue? retryAfter)
    {
        var now = _timeProvider.GetUtcNow();
        _consecutiveTransientFailures = Math.Min(_consecutiveTransientFailures + 1, 6);

        TimeSpan delay;
        if (retryAfter?.Delta is { } delta)
        {
            delay = delta;
        }
        else if (retryAfter?.Date is { } date)
        {
            delay = date - now;
        }
        else
        {
            var seconds = Math.Min(MaximumBackoff.TotalSeconds,
                BaseBackoffSeconds * Math.Pow(2, _consecutiveTransientFailures - 1));
            delay = TimeSpan.FromSeconds(seconds);
        }

        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
        if (delay > MaximumBackoff) delay = MaximumBackoff;

        var retryAt = now + delay;
        if (retryAt > _backoffUntilUtc) _backoffUntilUtc = retryAt;
    }
}
