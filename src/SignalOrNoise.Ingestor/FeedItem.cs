namespace SignalOrNoise.Ingestor;

public record FeedItem(string Title, string Url, string? Summary, DateTime PublishedUtc);
