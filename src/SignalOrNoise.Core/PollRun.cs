namespace SignalOrNoise.Core;

// One row per feed per poll, so gaps in collection (laptop asleep, feed down) are visible in the data.
public class PollRun
{
    public long Id { get; set; }
    public string Feed { get; set; } = "";
    public DateTime StartedUtc { get; set; }
    public int ItemsFetched { get; set; }
    public int NewHeadlines { get; set; }
    public string? Error { get; set; }
}
