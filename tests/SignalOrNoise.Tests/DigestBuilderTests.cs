using SignalOrNoise.Api;

namespace SignalOrNoise.Tests;

public class DigestBuilderTests
{
    private static readonly DateTime Noon = new(2026, 10, 20, 16, 0, 0, DateTimeKind.Utc);

    private static ScoredStory Story(long id, string? ticker, string eventType, int materiality, int headlines = 1, int minutes = 0) =>
        new(id, Noon.AddMinutes(minutes), materiality, 0, eventType, ticker, $"summary {id}",
            Enumerable.Range(0, headlines)
                .Select(i => new HeadlineLink($"title {id}.{i}", $"https://example.com/{id}/{i}", "yahoo", Noon.AddMinutes(minutes + i)))
                .ToList());

    [Fact]
    public void Stories_with_the_same_ticker_and_event_type_become_one_row_led_by_the_highest_score()
    {
        var rows = DigestBuilder.Build([
            Story(1, "TSLA", "m_and_a", 6, headlines: 7),
            Story(2, "TSLA", "m_and_a", 7, headlines: 2, minutes: 80),
            Story(3, "TSLA", "m_and_a", 5, minutes: 30),
        ], size: 30);

        var row = Assert.Single(rows);
        Assert.Equal(2, row.Id);
        Assert.Equal(7, row.Materiality);
        Assert.Equal(10, row.HeadlineCount);
        Assert.Equal(3, row.StoryCount);
        Assert.Equal(Noon, row.FirstPublishedUtc);
        Assert.Equal(["title 1.0", "title 1.1", "title 1.2"], row.Links.Select(l => l.Title));
    }

    [Fact]
    public void Different_event_types_for_the_same_ticker_stay_separate()
    {
        var rows = DigestBuilder.Build([Story(1, "NFLX", "earnings", 7), Story(2, "NFLX", "opinion", 2)], size: 30);

        Assert.Equal([1L, 2L], rows.Select(r => r.Id));
    }

    [Fact]
    public void Market_wide_stories_are_never_merged()
    {
        var rows = DigestBuilder.Build([Story(1, null, "macro", 3), Story(2, null, "macro", 3)], size: 30);

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void Rows_rank_by_materiality_then_headline_count_and_are_capped_at_size()
    {
        var rows = DigestBuilder.Build([
            Story(1, "AAPL", "product", 3),
            Story(2, "MSFT", "earnings", 8),
            Story(3, "NVDA", "product", 3, headlines: 4),
            Story(4, "KO", "opinion", 1),
        ], size: 3);

        Assert.Equal([2L, 3L, 1L], rows.Select(r => r.Id));
    }
}
