namespace SignalOrNoise.Api;

// JSON shapes returned by the API (serialized as camelCase).

public record DigestResponse(DateOnly Date, int StoryCount, List<DigestStory> Stories);

// One digest row. It can stand for several stories about the same event (StoryCount > 1); Id is the lead story.
public record DigestStory(
    long Id,
    DateTime FirstPublishedUtc,
    int Materiality,
    int Direction,
    string EventType,
    string? PrimaryTicker,
    string Summary,
    int HeadlineCount,
    int StoryCount,
    List<HeadlineLink> Links);

public record HeadlineLink(string Title, string Url, string Source, DateTime PublishedUtc);

public record StoryDetail(
    long Id,
    DateTime FirstPublishedUtc,
    DateTime? ScoredAsOfUtc,
    int? Materiality,
    int? Direction,
    string? EventType,
    string? PrimaryTicker,
    string? Summary,
    string? Rationale,
    string? Model,
    List<StoryHeadline> Headlines);

// SeenByScorer: published at or before the score's cutoff, so it was part of the prompt.
public record StoryHeadline(string Title, string Url, string Source, string Tickers, DateTime PublishedUtc, bool SeenByScorer);
