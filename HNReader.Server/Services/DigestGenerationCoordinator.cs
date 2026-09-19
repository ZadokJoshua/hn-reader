using Hangfire;

namespace HNReader.Server.Services;

public enum DigestGenerationOutcome
{
    /// <summary>Today's digest already exists; nothing was started.</summary>
    AlreadyCurrent,
    /// <summary>A run was enqueued by this request.</summary>
    Started,
    /// <summary>A run was already under way; this request did not start another.</summary>
    InProgress
}

/// <summary>
/// Serves "make sure today's digest exists" requests from clients.
///
/// <para>
/// A digest run costs real money and takes minutes, so this exists to make the
/// request <b>idempotent and cost-bounded</b>: it starts a run only when there
/// is genuinely no digest for the current UTC day and no run already in flight.
/// The consequence worth stating plainly — no matter how many times, or by how
/// many clients, this is called, it cannot cost more than the one nightly run
/// Hangfire already performs.
/// </para>
///
/// <para>
/// "Today" is the current <b>UTC</b> date, matching the cron schedule the
/// nightly job runs on. Using local time would make the answer depend on where
/// the caller is standing, and two clients in different zones would disagree
/// about whether a digest was current.
/// </para>
/// </summary>
public class DigestGenerationCoordinator
{
    /// <summary>
    /// How long an in-flight run is believed to still be running. A process that
    /// dies mid-run can't clear the flag, and without an expiry that would wedge
    /// generation until the next restart. Comfortably longer than a measured run
    /// (~10 minutes worst case).
    /// </summary>
    private static readonly TimeSpan InFlightExpiry = TimeSpan.FromMinutes(45);

    private readonly DigestStorageService _storage;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<DigestGenerationCoordinator> _logger;
    private readonly object _gate = new();

    private DateTimeOffset? _runStartedAtUtc;

    public DigestGenerationCoordinator(
        DigestStorageService storage,
        IBackgroundJobClient backgroundJobs,
        ILogger<DigestGenerationCoordinator> logger)
    {
        _storage = storage;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    /// <summary>True while a run started through this coordinator is believed to be running.</summary>
    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return IsRunningUnderGate();
            }
        }
    }

    public async Task<DigestGenerationOutcome> EnsureTodaysDigestAsync()
    {
        var latest = await _storage.GetLatestAsync().ConfigureAwait(false);

        if (latest is not null && latest.GeneratedAtUtc.Date == DateTime.UtcNow.Date)
        {
            return DigestGenerationOutcome.AlreadyCurrent;
        }

        lock (_gate)
        {
            if (IsRunningUnderGate())
            {
                return DigestGenerationOutcome.InProgress;
            }

            _runStartedAtUtc = DateTimeOffset.UtcNow;
        }

        // Enqueued rather than awaited: a run takes minutes, and an HTTP request
        // is the wrong place to hold that. Hangfire also gives it the same retry
        // and history treatment as the nightly run.
        _backgroundJobs.Enqueue<DigestJob>(job => job.RunAsync(CancellationToken.None));
        _logger.LogInformation("Digest: generation requested by a client; run enqueued");

        return DigestGenerationOutcome.Started;
    }

    /// <summary>
    /// Called by <see cref="DigestJob"/> when a run ends, successfully or not, so
    /// the next request isn't told a run is in progress when none is.
    /// </summary>
    public void MarkRunFinished()
    {
        lock (_gate)
        {
            _runStartedAtUtc = null;
        }
    }

    private bool IsRunningUnderGate() =>
        _runStartedAtUtc is { } startedAt && DateTimeOffset.UtcNow - startedAt < InFlightExpiry;
}
