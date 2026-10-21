using Microsoft.Extensions.Options;

namespace SignalOrNoise.Ingestor;

public class ScoringWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ScoringOptions> options,
    ILogger<ScoringWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.IntervalMinutes));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var scoring = scope.ServiceProvider.GetRequiredService<ScoringService>();
                await scoring.ClusterAsync(stoppingToken);
                var scored = await scoring.ScoreDueAsync(stoppingToken);
                logger.LogInformation("Scoring run finished: {ScoredCount} stories scored", scored);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Log and keep the host running, so ingestion continues; the next run retries.
                logger.LogError(ex, "Scoring run failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
