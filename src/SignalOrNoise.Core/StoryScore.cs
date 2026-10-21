namespace SignalOrNoise.Core;

// One LLM verdict on a story, with everything needed to reproduce and cost it.
public class StoryScore
{
    public long Id { get; set; }
    public long StoryId { get; set; }
    public Story? Story { get; set; }

    // SHA-256 of model + prompt version + the exact prompt sent. Same input => reuse this score instead of re-billing.
    public string InputHash { get; set; } = "";
    public string Model { get; set; } = "";
    public string PromptVersion { get; set; } = "";

    // The cutoff actually used for this score (the story's cutoff can move if an earlier headline arrives later).
    public DateTime ScoredAsOfUtc { get; set; }
    public int HeadlineCount { get; set; }

    public string EventType { get; set; } = "";
    public string? PrimaryTicker { get; set; }
    public int Materiality { get; set; }
    public int Direction { get; set; }
    public string Summary { get; set; } = "";
    public string Rationale { get; set; } = "";

    public int Attempts { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal CostUsd { get; set; }
    public int LatencyMs { get; set; }
    public bool FromCache { get; set; }
    public DateTime CreatedUtc { get; set; }
}
