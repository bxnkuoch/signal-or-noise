using Microsoft.EntityFrameworkCore;
using SignalOrNoise.Api;
using SignalOrNoise.Core;

const int DigestSize = 30;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Set the ConnectionStrings__Default environment variable (see README).");
builder.Services.AddDbContext<NewsDbContext>(options =>
    options.UseSqlServer(connectionString).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

var app = builder.Build();

app.MapGet("/health", async (NewsDbContext db) =>
    await db.Database.CanConnectAsync() ? Results.Ok("ok") : Results.Problem("Database unreachable"));

// Top events by materiality for one US/Eastern day (default: today). See DigestBuilder for grouping and ranking.
// A day has a few hundred stories at most, so they are grouped in memory.
app.MapGet("/digest", async (DateOnly? date, NewsDbContext db) =>
{
    var day = date ?? MarketDay.Today(DateTime.UtcNow);
    var (fromUtc, toUtc) = MarketDay.UtcRange(day);

    var stories = await db.Stories
        .Where(s => s.FirstPublishedUtc >= fromUtc && s.FirstPublishedUtc < toUtc)
        .Select(s => new { Story = s, Score = s.Scores.OrderByDescending(sc => sc.CreatedUtc).FirstOrDefault() })
        .Where(x => x.Score != null)
        .Select(x => new ScoredStory(
            x.Story.Id,
            x.Story.FirstPublishedUtc,
            x.Score!.Materiality,
            x.Score.Direction,
            x.Score.EventType,
            x.Score.PrimaryTicker,
            x.Score.Summary,
            x.Story.Headlines
                .Select(h => new HeadlineLink(h.Title, h.Url, h.Source, h.PublishedUtc))
                .ToList()))
        .ToListAsync();

    return new DigestResponse(day, stories.Count, DigestBuilder.Build(stories, DigestSize));
});

// One story with every member headline, marking which ones the scorer saw.
app.MapGet("/stories/{id:long}", async (long id, NewsDbContext db) =>
{
    var story = await db.Stories
        .Include(s => s.Headlines)
        .Include(s => s.Scores)
        .FirstOrDefaultAsync(s => s.Id == id);
    if (story is null) return Results.NotFound();

    var score = story.Scores.MaxBy(sc => sc.CreatedUtc);
    return Results.Ok(new StoryDetail(
        story.Id,
        story.FirstPublishedUtc,
        score?.ScoredAsOfUtc,
        score?.Materiality,
        score?.Direction,
        score?.EventType,
        score?.PrimaryTicker,
        score?.Summary,
        score?.Rationale,
        score?.Model,
        story.Headlines
            .OrderBy(h => h.PublishedUtc)
            .Select(h => new StoryHeadline(h.Title, h.Url, h.Source, h.Tickers, h.PublishedUtc,
                score is not null && h.PublishedUtc <= score.ScoredAsOfUtc))
            .ToList()));
});

app.Run();
