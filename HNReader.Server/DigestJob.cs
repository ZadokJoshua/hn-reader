using HNReader.Server.Services;
using HNReader.Shared.Models;

namespace HNReader.Server;

/// <summary>
/// Invoked by Hangfire's recurring job registration (see Program.cs) — Hangfire
/// owns scheduling now, this class just does the work when told to.
/// </summary>
public class DigestJob(
    DigestAIService digestAIService,
    DigestStorageService storage,
    DigestGenerationCoordinator coordinator,
    ILogger<DigestJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Digest job starting at {Time}", DateTimeOffset.UtcNow);

        try
        {
            var digest = await digestAIService.GenerateDailyDigestAsync(cancellationToken).ConfigureAwait(false);
            await storage.SaveAsync(digest).ConfigureAwait(false);

            logger.LogInformation(
                "Digest job finished: {CategoryCount} categories generated", digest.Categories.Count());
        }
        finally
        {
            // In a finally, not after the happy path: a failed run that left the
            // coordinator believing a run was still in flight would block every
            // later request until the flag's expiry elapsed.
            coordinator.MarkRunFinished();
        }
    }
}
