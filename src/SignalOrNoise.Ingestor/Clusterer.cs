using SignalOrNoise.Core;

namespace SignalOrNoise.Ingestor;

// Groups headlines about the same event without an LLM: same ticker, within a day, similar title words.
public static class Clusterer
{
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    // The scorer sees only headlines from the first 30 minutes of a story.
    public static readonly TimeSpan ScoringWindow = TimeSpan.FromMinutes(30);

    // Minimum Jaccard similarity of title words for a headline to join a story.
    public const double MinSimilarity = 0.3;

    // Words that appear in most financial headlines and say nothing about which event it is.
    private static readonly HashSet<string> StopWords =
    [
        "a", "an", "and", "are", "as", "at", "be", "by", "for", "from", "has", "have", "how", "in", "is", "it",
        "its", "of", "on", "or", "s", "that", "the", "this", "to", "what", "why", "will", "with", "you", "your",
        "after", "amid", "over", "says", "new", "stock", "stocks", "shares", "inc", "corp", "co",
    ];

    // Adds each unassigned headline, in publish order, to the most similar open story, or starts a new story.
    // A story is open to a headline if it started within Window of it and has a member sharing a ticker.
    // Returns the stories it created; existing stories are updated in place.
    public static List<Story> Assign(IEnumerable<Story> openStories, IEnumerable<Headline> unassigned)
    {
        var stories = openStories.ToList();
        var created = new List<Story>();
        foreach (var headline in unassigned.OrderBy(h => h.PublishedUtc))
        {
            var words = Words(headline.Title);
            var best = stories
                .Where(s => (headline.PublishedUtc - s.FirstPublishedUtc).Duration() <= Window)
                .Select(s => (Story: s, Similarity: s.Headlines
                    .Where(member => SharesTicker(member, headline))
                    .Select(member => Jaccard(words, Words(member.Title)))
                    .DefaultIfEmpty(0)
                    .Max()))
                .Where(match => match.Similarity >= MinSimilarity)
                .OrderByDescending(match => match.Similarity)
                .FirstOrDefault().Story;

            if (best is null)
            {
                best = new Story();
                stories.Add(best);
                created.Add(best);
            }

            best.Headlines.Add(headline);
            if (best.Headlines.Count == 1 || headline.PublishedUtc < best.FirstPublishedUtc)
            {
                best.FirstPublishedUtc = headline.PublishedUtc;
                best.ScoredAsOfUtc = headline.PublishedUtc + ScoringWindow;
            }
        }
        return created;
    }

    public static HashSet<string> Words(string title) =>
        HeadlineNormalizer.NormalizeTitle(title).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => !StopWords.Contains(word))
            .ToHashSet();

    public static double Jaccard(HashSet<string> a, HashSet<string> b)
    {
        var union = a.Union(b).Count();
        return union == 0 ? 0 : (double)a.Intersect(b).Count() / union;
    }

    // Untagged headlines (general market feeds) only cluster with other untagged headlines.
    private static bool SharesTicker(Headline a, Headline b)
    {
        var aTickers = a.Tickers.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var bTickers = b.Tickers.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return aTickers.Length == 0 && bTickers.Length == 0 || aTickers.Intersect(bTickers).Any();
    }
}
