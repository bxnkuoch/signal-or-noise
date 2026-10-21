namespace SignalOrNoise.Api;

public record ScoredStory(
    long Id,
    DateTime FirstPublishedUtc,
    int Materiality,
    int Direction,
    string EventType,
    string? PrimaryTicker,
    string Summary,
    List<HeadlineLink> Headlines);

public static class DigestBuilder
{
    public const int LinksPerRow = 3;

    // Title-word clustering misses rewordings of the same event ("Tesla, SpaceX form merger committees" vs
    // "Musk backs SpaceX-Tesla merger"), so stories the scorer gave the same primary ticker and event type
    // become one row. Market-wide stories (no primary ticker) are never merged.
    // Rows are ranked by materiality, then headline count, then recency.
    public static List<DigestStory> Build(IEnumerable<ScoredStory> stories, int size) =>
        stories
            .GroupBy(s => s.PrimaryTicker is null ? $"story:{s.Id}" : $"{s.PrimaryTicker}:{s.EventType}")
            .Select(ToRow)
            .OrderByDescending(row => row.Materiality)
            .ThenByDescending(row => row.HeadlineCount)
            .ThenByDescending(row => row.FirstPublishedUtc)
            .Take(size)
            .ToList();

    private static DigestStory ToRow(IEnumerable<ScoredStory> group)
    {
        var members = group.ToList();
        // The row shows the highest-scored story; ties go to the one with more headlines, then the earliest.
        var lead = members
            .OrderByDescending(s => s.Materiality)
            .ThenByDescending(s => s.Headlines.Count)
            .ThenBy(s => s.FirstPublishedUtc)
            .First();
        var headlines = members.SelectMany(s => s.Headlines).OrderBy(h => h.PublishedUtc).ToList();

        return new DigestStory(
            lead.Id,
            members.Min(s => s.FirstPublishedUtc),
            lead.Materiality,
            lead.Direction,
            lead.EventType,
            lead.PrimaryTicker,
            lead.Summary,
            headlines.Count,
            members.Count,
            headlines.Take(LinksPerRow).ToList());
    }
}
