namespace HNReader.Server;

public class DigestWorkerService(ILogger<DigestWorkerService> logger) : BackgroundService
{
    private readonly ILogger<DigestWorkerService> _logger = logger;
    private readonly TimeSpan _targetUtcRunTime = new(0, 0, 0);

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay = CalculateNextUtcDelay();
            _logger.LogInformation("Next digest creation scheduled at 12 AM UTC. Waiting for {Delay}", delay);

            try
            {
                await Task.Delay(delay, stoppingToken);

                _logger.LogInformation("Executing daily digest creation at UTC time: {Time}", DateTimeOffset.UtcNow);
                await GenerateDailyDigest(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Daily digest background service is stopping.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during daily digest creation.");
            }
        }
    }

    private TimeSpan CalculateNextUtcDelay()
    {
        DateTime nowUtc = DateTime.UtcNow;
        DateTime nextRunUtc = nowUtc.Date.Add(_targetUtcRunTime);

        // If midnight UTC has already passed today, target midnight tomorrow
        if (nowUtc >= nextRunUtc) nextRunUtc = nextRunUtc.AddDays(1);
        return nextRunUtc - nowUtc;
    }

    private async Task GenerateDailyDigest(CancellationToken stoppingToken)
    {
        // Insert your daily workload here
        await Task.Delay(1000, stoppingToken);
    }
}
