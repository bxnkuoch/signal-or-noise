using SignalOrNoise.Core;
using SignalOrNoise.Ingestor;

namespace SignalOrNoise.Tests;

public class DeduperTests
{
    private static readonly DateTime Noon = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Headline Make(string title, string url, string ticker, DateTime? published = null) =>
        HeadlineNormalizer.ToHeadline(new FeedItem(title, url, null, published ?? Noon), "yahoo", ticker, Noon);

    [Fact]
    public void Same_url_in_two_ticker_feeds_is_stored_once_with_both_tickers()
    {
        var added = Deduper.Merge([], [
            Make("Apple and Microsoft rally", "https://example.com/a", "MSFT"),
            Make("Apple and Microsoft rally", "https://example.com/a?utm_source=rss", "AAPL"),
        ]);

        var headline = Assert.Single(added);
        Assert.Equal("AAPL,MSFT", headline.Tickers);
    }

    [Fact]
    public void Headline_already_in_the_database_is_not_added_again_but_gains_the_new_ticker()
    {
        var existing = Make("Apple and Microsoft rally", "https://example.com/a", "AAPL");

        var added = Deduper.Merge([existing], [Make("Apple and Microsoft rally", "https://example.com/a", "MSFT")]);

        Assert.Empty(added);
        Assert.Equal("AAPL,MSFT", existing.Tickers);
    }

    [Fact]
    public void Same_title_at_a_different_url_within_the_window_is_a_duplicate()
    {
        var added = Deduper.Merge([], [
            Make("Nvidia Beats Estimates", "https://site-one.com/nvda", "NVDA"),
            Make("NVIDIA beats estimates!", "https://site-two.com/story/123", "NVDA", Noon.AddHours(5)),
        ]);

        Assert.Single(added);
    }

    [Fact]
    public void Same_title_on_different_days_is_kept_as_separate_headlines()
    {
        var added = Deduper.Merge([], [
            Make("Stocks making the biggest moves midday", "https://example.com/day1", ""),
            Make("Stocks making the biggest moves midday", "https://example.com/day2", "", Noon.AddDays(1)),
        ]);

        Assert.Equal(2, added.Count);
    }

    [Fact]
    public void Merging_the_same_batch_twice_adds_nothing_the_second_time()
    {
        Headline[] Batch() =>
        [
            Make("Apple downgraded", "https://example.com/1", "AAPL"),
            Make("Tesla deliveries miss", "https://example.com/2", "TSLA"),
        ];
        var stored = Deduper.Merge([], Batch());

        var addedAgain = Deduper.Merge(stored, Batch());

        Assert.Equal(2, stored.Count);
        Assert.Empty(addedAgain);
        Assert.Equal(["AAPL", "TSLA"], stored.Select(h => h.Tickers));
    }

    [Theory]
    [InlineData("", "AAPL", "AAPL")]
    [InlineData("MSFT", "AAPL", "AAPL,MSFT")]
    [InlineData("AAPL,MSFT", "MSFT", "AAPL,MSFT")]
    [InlineData("AAPL", "", "AAPL")]
    public void MergeTickers_unions_and_sorts(string a, string b, string expected)
    {
        Assert.Equal(expected, Deduper.MergeTickers(a, b));
    }
}
