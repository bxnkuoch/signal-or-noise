namespace SignalOrNoise.Ingestor;

public record Feed(string Name, string Source, string Url, string? Ticker);

public class IngestOptions
{
    public int PollIntervalMinutes { get; set; } = 15;

    // Source name -> URL template containing {ticker}.
    public Dictionary<string, string> TickerFeeds { get; set; } = [];

    // Source name -> URL. Headlines from these have no ticker.
    public Dictionary<string, string> MarketFeeds { get; set; } = [];

    public List<string> Tickers { get; set; } = [];

    public List<Feed> BuildFeeds() =>
    [
        .. Tickers.SelectMany(ticker => TickerFeeds.Select(feed =>
            new Feed($"{feed.Key}:{ticker}", feed.Key, feed.Value.Replace("{ticker}", ticker), ticker))),
        .. MarketFeeds.Select(feed => new Feed(feed.Key, feed.Key, feed.Value, null)),
    ];
}
