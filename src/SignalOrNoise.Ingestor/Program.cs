using Microsoft.EntityFrameworkCore;
using SignalOrNoise.Core;
using SignalOrNoise.Ingestor;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Set the ConnectionStrings__Default environment variable (see README).");
builder.Services.AddDbContext<NewsDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.Configure<IngestOptions>(builder.Configuration.GetSection("Ingest"));
builder.Services.AddSingleton(_ =>
{
    // One long-lived HttpClient; PooledConnectionLifetime makes it pick up DNS changes.
    var http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(15) })
    {
        Timeout = Timeout.InfiniteTimeSpan, // FeedClient's Polly pipeline owns timeouts.
    };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("SignalOrNoise/0.1 (+https://github.com/bxnkuoch/signal-or-noise)");
    return new FeedClient(http);
});
builder.Services.AddScoped<IngestService>();
builder.Services.AddHostedService<IngestWorker>();

builder.Build().Run();
