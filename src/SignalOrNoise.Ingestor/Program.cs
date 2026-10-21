using Anthropic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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

// Clustering and scoring need the Claude API. Without a key, the service still ingests.
var scoringEnabled = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
if (scoringEnabled)
{
    builder.Services.Configure<ScoringOptions>(builder.Configuration.GetSection("Scoring"));
    builder.Services.AddSingleton<ILlmClient>(sp =>
        new AnthropicLlmClient(new AnthropicClient(), sp.GetRequiredService<IOptions<ScoringOptions>>().Value.Model));
    builder.Services.AddSingleton(sp => new StoryScorer(
        sp.GetRequiredService<ILlmClient>(),
        sp.GetRequiredService<IOptions<ScoringOptions>>().Value,
        sp.GetRequiredService<IOptions<IngestOptions>>().Value.Tickers,
        sp.GetRequiredService<ILogger<StoryScorer>>()));
    builder.Services.AddScoped<ScoringService>();
    builder.Services.AddHostedService<ScoringWorker>();
}

var host = builder.Build();
if (!scoringEnabled)
    host.Services.GetRequiredService<ILogger<Program>>()
        .LogWarning("ANTHROPIC_API_KEY is not set, so clustering and scoring are off. Ingestion still runs.");
host.Run();
