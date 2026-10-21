namespace SignalOrNoise.Core;

// A group of headlines about the same event.
public class Story
{
    public long Id { get; set; }
    public DateTime FirstPublishedUtc { get; set; }

    // No lookahead: the scorer only sees member headlines published at or before this time.
    // Headlines published later still join the story for display.
    public DateTime ScoredAsOfUtc { get; set; }

    public List<Headline> Headlines { get; set; } = [];
    public List<StoryScore> Scores { get; set; } = [];
}
