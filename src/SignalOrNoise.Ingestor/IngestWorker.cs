using Microsoft.EntityFrameworkCore;
using SignalOrNoise.Core;

namespace SignalOrNoise.Ingestor;

// M0: proves the service can reach SQL Server, then exits. M1 replaces this with the RSS polling loop.
public class IngestWorker(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime lifetime,
    ILogger<IngestWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NewsDbContext>();

        var count = await db.Headlines.CountAsync(stoppingToken);
        logger.LogInformation("Connected to SQL Server. {HeadlineCount} headlines stored", count);

        lifetime.StopApplication();
    }
}
