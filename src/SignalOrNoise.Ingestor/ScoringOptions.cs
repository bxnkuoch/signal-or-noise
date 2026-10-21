namespace SignalOrNoise.Ingestor;

public class ScoringOptions
{
    public string Model { get; set; } = "claude-haiku-5-5";
    public int IntervalMinutes { get; set; } = 15;

    // Caps how many stories one run scores, so a large backlog is spread over several runs.
    public int MaxStoriesPerRun { get; set; } = 200;

    // Published per-million-token prices for Model, used to log cost per call.
    public decimal InputUsdPerMTok { get; set; }
    public decimal OutputUsdPerMTok { get; set; }
}
