using SignalOrNoise.Ingestor;

namespace SignalOrNoise.Tests;

public class IngestOptionsTests
{
    [Fact]
    public void BuildFeeds_makes_one_feed_per_ticker_per_template_plus_market_feeds()
    {
        var options = new IngestOptions
        {
            TickerFeeds = { ["yahoo"] = "https://feeds.example.com/rss?s={ticker}" },
            MarketFeeds = { ["cnbc"] = "https://cnbc.example.com/rss" },
            Tickers = ["AAPL", "MSFT"],
        };

        var feeds = options.BuildFeeds();

        Assert.Equal(
        [
            new Feed("yahoo:AAPL", "yahoo", "https://feeds.example.com/rss?s=AAPL", "AAPL"),
            new Feed("yahoo:MSFT", "yahoo", "https://feeds.example.com/rss?s=MSFT", "MSFT"),
            new Feed("cnbc", "cnbc", "https://cnbc.example.com/rss", null),
        ], feeds);
    }
}
