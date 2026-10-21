namespace SignalOrNoise.Core;

public class Headline
{
    public long Id { get; set; }
    public string Source { get; set; } = "";
    public string Url { get; set; } = "";

    // SHA-256 hex of the normalized URL. Indexed instead of Url because SQL Server
    // index keys max out at 1700 bytes and URLs can be longer.
    public string UrlHash { get; set; } = "";

    public string Title { get; set; } = "";

    // SHA-256 hex of the normalized title, used to catch the same story syndicated under different URLs.
    public string TitleHash { get; set; } = "";

    public string? Summary { get; set; }
    public DateTime PublishedUtc { get; set; }
    public DateTime IngestedUtc { get; set; }

    // Comma-separated, e.g. "AAPL,MSFT". Simple to store; a join table can come later if queries need it.
    public string Tickers { get; set; } = "";

    // Null until clustering assigns the headline to a story.
    public long? StoryId { get; set; }
    public Story? Story { get; set; }
}
