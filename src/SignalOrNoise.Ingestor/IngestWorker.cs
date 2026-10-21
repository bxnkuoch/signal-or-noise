using Microsoft.Extensions.Options;

namespace SignalOrNoise.Ingestor;

public class IngestWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<IngestOptions> options,
    ILogger<IngestWorker> logger) : BackgroundService
{
    // Pause between requests so we don't hammer the feed hosts.
    private static readonly TimeSpan RequestSpacing = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var feeds = options.Value.BuildFeeds();
        var interval = TimeSpan.FromMinutes(options.Value.PollIntervalMinutes);
        logger.LogInformation("Polling {FeedCount} feeds every {Interval}", feeds.Count, interval);

        using var timer = new PeriodicTimer(interval);
        do
        {
            var newHeadlines = 0;
            foreach (var feed in feeds)
            {
                // A fresh scope (and DbContext) per feed keeps each poll's change tracking small and isolated.
                using var scope = scopeFactory.CreateScope();
                newHeadlines += await scope.ServiceProvider.GetRequiredService<IngestService>().PollAsync(feed, stoppingToken);
                await Task.Delay(RequestSpacing, stoppingToken);
            }
            logger.LogInformation("Poll cycle finished: {NewHeadlines} new headlines from {FeedCount} feeds",
                newHeadlines, feeds.Count);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
