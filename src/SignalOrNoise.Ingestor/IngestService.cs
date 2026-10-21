using Microsoft.EntityFrameworkCore;
using SignalOrNoise.Core;

namespace SignalOrNoise.Ingestor;

public class IngestService(NewsDbContext db, FeedClient feedClient, ILogger<IngestService> logger)
{
    // Fetches one feed and stores its new headlines. Always records a PollRun, including on failure.
    // Returns the number of new headlines.
    public async Task<int> PollAsync(Feed feed, CancellationToken ct)
    {
        var run = new PollRun { Feed = feed.Name, StartedUtc = DateTime.UtcNow };
        try
        {
            var xml = await feedClient.GetAsync(feed.Url, ct);
            var now = DateTime.UtcNow;
            var incoming = RssParser.Parse(xml)
                .Select(item => HeadlineNormalizer.ToHeadline(item, feed.Source, feed.Ticker, now))
                .ToList();

            var urlHashes = incoming.Select(h => h.UrlHash).ToList();
            var titleHashes = incoming.Select(h => h.TitleHash).ToList();
            var existing = await db.Headlines
                .Where(h => urlHashes.Contains(h.UrlHash) || titleHashes.Contains(h.TitleHash))
                .ToListAsync(ct);

            var added = Deduper.Merge(existing, incoming);
            db.Headlines.AddRange(added);
            await db.SaveChangesAsync(ct);

            run.ItemsFetched = incoming.Count;
            run.NewHeadlines = added.Count;
            logger.LogInformation("Polled {Feed}: {ItemsFetched} items, {NewHeadlines} new",
                feed.Name, run.ItemsFetched, run.NewHeadlines);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            db.ChangeTracker.Clear();
            run.Error = ex.Message;
            logger.LogWarning(ex, "Polling {Feed} failed", feed.Name);
        }

        db.PollRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return run.NewHeadlines;
    }
}
