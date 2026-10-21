using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SignalOrNoise.Core;

namespace SignalOrNoise.Ingestor;

public class ScoringService(
    NewsDbContext db,
    StoryScorer scorer,
    IOptions<ScoringOptions> options,
    ILogger<ScoringService> logger)
{
    // Wait this long past a story's cutoff before scoring it, so headlines published just before
    // the cutoff have been through a poll (every 15 minutes) and are included.
    private static readonly TimeSpan IngestGrace = TimeSpan.FromMinutes(20);

    public async Task ClusterAsync(CancellationToken ct)
    {
        var unassigned = await db.Headlines
            .Where(h => h.StoryId == null)
            .OrderBy(h => h.PublishedUtc)
            .ToListAsync(ct);
        if (unassigned.Count == 0) return;

        var since = unassigned[0].PublishedUtc - Clusterer.Window;
        var open = await db.Stories
            .Include(s => s.Headlines)
            .Where(s => s.FirstPublishedUtc >= since)
            .ToListAsync(ct);

        var created = Clusterer.Assign(open, unassigned);
        db.Stories.AddRange(created);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Clustered {HeadlineCount} headlines into {NewStoryCount} new stories",
            unassigned.Count, created.Count);
    }

    // Scores stories whose cutoff has passed and that have no score for the current model and prompt version.
    // Returns the number of stories scored.
    public async Task<int> ScoreDueAsync(CancellationToken ct)
    {
        var dueBy = DateTime.UtcNow - IngestGrace;
        var model = scorer.Model;
        var stories = await db.Stories
            .Include(s => s.Headlines)
            .Where(s => s.ScoredAsOfUtc <= dueBy
                && !s.Scores.Any(sc => sc.Model == model && sc.PromptVersion == StoryScorer.PromptVersion))
            .OrderByDescending(s => s.FirstPublishedUtc)
            .Take(options.Value.MaxStoriesPerRun)
            .ToListAsync(ct);

        var scoredCount = 0;
        foreach (var story in stories)
        {
            var input = StoryScorer.SelectInput(story.Headlines, story.ScoredAsOfUtc);
            var score = new StoryScore
            {
                StoryId = story.Id,
                InputHash = scorer.InputHash(input),
                Model = model,
                PromptVersion = StoryScorer.PromptVersion,
                ScoredAsOfUtc = story.ScoredAsOfUtc,
                HeadlineCount = input.Count,
                CreatedUtc = DateTime.UtcNow,
            };

            var cached = await db.StoryScores.AsNoTracking().FirstOrDefaultAsync(sc => sc.InputHash == score.InputHash, ct);
            if (cached is not null)
            {
                CopyVerdict(cached, score);
                score.FromCache = true;
            }
            else
            {
                try
                {
                    var result = await scorer.ScoreAsync(input, ct);
                    ApplyResult(result, score);
                }
                catch (ScoringException ex)
                {
                    // Not saved, so the story is retried on the next run.
                    logger.LogWarning("Could not score story {StoryId}: {Reason}", story.Id, ex.Message);
                    continue;
                }
            }

            // Save each score as it arrives, so a crash mid-run doesn't lose results that were paid for.
            db.StoryScores.Add(score);
            await db.SaveChangesAsync(ct);
            scoredCount++;
            logger.LogInformation(
                "Scored story {StoryId}: materiality {Materiality} {EventType} {PrimaryTicker}, " +
                "{InputTokens}+{OutputTokens} tokens, ${CostUsd}, {LatencyMs} ms, cached {FromCache}",
                story.Id, score.Materiality, score.EventType, score.PrimaryTicker,
                score.InputTokens, score.OutputTokens, score.CostUsd, score.LatencyMs, score.FromCache);
        }
        return scoredCount;
    }

    private static void ApplyResult(ScoreResult result, StoryScore score)
    {
        var verdict = result.Verdict;
        score.EventType = verdict.EventType;
        score.PrimaryTicker = verdict.PrimaryTicker == StoryScorer.NoTicker ? null : verdict.PrimaryTicker;
        score.Materiality = verdict.Materiality;
        score.Direction = verdict.Direction;
        score.Summary = verdict.OneLineSummary;
        score.Rationale = verdict.Rationale;
        score.Attempts = result.Attempts;
        score.InputTokens = result.InputTokens;
        score.OutputTokens = result.OutputTokens;
        score.CostUsd = result.CostUsd;
        score.LatencyMs = (int)result.Latency.TotalMilliseconds;
    }

    // A cache hit costs nothing, so tokens, cost, and latency stay zero.
    private static void CopyVerdict(StoryScore from, StoryScore to)
    {
        to.EventType = from.EventType;
        to.PrimaryTicker = from.PrimaryTicker;
        to.Materiality = from.Materiality;
        to.Direction = from.Direction;
        to.Summary = from.Summary;
        to.Rationale = from.Rationale;
    }
}
