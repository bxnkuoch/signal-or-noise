using SignalOrNoise.Core;
using SignalOrNoise.Ingestor;

namespace SignalOrNoise.Tests;

public class ClustererTests
{
    private static readonly DateTime Noon = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Headline Make(string title, string tickers, DateTime published) =>
        new() { Title = title, Tickers = tickers, PublishedUtc = published };

    [Fact]
    public void Similar_headlines_about_the_same_ticker_form_one_story()
    {
        var stories = Clusterer.Assign([], [
            Make("Loop Capital downgrades Apple on slow iPhone sales", "AAPL", Noon),
            Make("Apple downgraded by Loop Capital on iPhone sales fears", "AAPL", Noon.AddHours(2)),
        ]);

        Assert.Equal(2, Assert.Single(stories).Headlines.Count);
    }

    [Fact]
    public void Same_words_about_different_tickers_stay_separate()
    {
        var stories = Clusterer.Assign([], [
            Make("Analyst downgrades shares on slow sales", "AAPL", Noon),
            Make("Analyst downgrades shares on slow sales", "NKE", Noon),
        ]);

        Assert.Equal(2, stories.Count);
    }

    [Fact]
    public void Similar_headlines_more_than_a_day_apart_stay_separate()
    {
        var stories = Clusterer.Assign([], [
            Make("Tesla deliveries miss estimates", "TSLA", Noon),
            Make("Tesla deliveries miss estimates", "TSLA", Noon.AddHours(25)),
        ]);

        Assert.Equal(2, stories.Count);
    }

    [Fact]
    public void Unrelated_headlines_about_the_same_ticker_stay_separate()
    {
        var stories = Clusterer.Assign([], [
            Make("Nvidia beats quarterly revenue estimates", "NVDA", Noon),
            Make("Nvidia CEO to speak at Computex keynote", "NVDA", Noon.AddHours(1)),
        ]);

        Assert.Equal(2, stories.Count);
    }

    [Fact]
    public void Untagged_market_headlines_cluster_only_with_each_other()
    {
        var stories = Clusterer.Assign([], [
            Make("Fed holds rates steady, signals cuts later this year", "", Noon),
            Make("Fed holds rates steady and signals cuts later this year", "", Noon.AddMinutes(10)),
            Make("Fed holds rates steady, signals cuts later this year", "JPM", Noon.AddMinutes(20)),
        ]);

        Assert.Equal([2, 1], stories.Select(s => s.Headlines.Count));
    }

    [Fact]
    public void New_headline_joins_an_existing_open_story()
    {
        var open = new Story { FirstPublishedUtc = Noon, ScoredAsOfUtc = Noon + Clusterer.ScoringWindow };
        open.Headlines.Add(Make("Microsoft to acquire gaming studio for $2 billion", "MSFT", Noon));

        var created = Clusterer.Assign([open], [Make("Microsoft agrees to acquire gaming studio", "MSFT", Noon.AddHours(3))]);

        Assert.Empty(created);
        Assert.Equal(2, open.Headlines.Count);
    }

    [Fact]
    public void Scoring_cutoff_is_thirty_minutes_after_the_earliest_headline_even_if_it_arrives_late()
    {
        var stories = Clusterer.Assign([], [Make("Boeing halts 737 deliveries after FAA review", "BA", Noon)]);
        var story = Assert.Single(stories);
        Assert.Equal(Noon.AddMinutes(30), story.ScoredAsOfUtc);

        Clusterer.Assign(stories, [Make("Boeing halts 737 deliveries pending FAA review", "BA", Noon.AddHours(-1))]);

        Assert.Equal(Noon.AddHours(-1), story.FirstPublishedUtc);
        Assert.Equal(Noon.AddMinutes(-30), story.ScoredAsOfUtc);
    }

    [Fact]
    public void Words_drops_stop_words_and_punctuation()
    {
        Assert.Equal(["apple", "downgraded", "loop", "capital"], Clusterer.Words("Apple Stock Downgraded by Loop Capital!"));
    }
}
