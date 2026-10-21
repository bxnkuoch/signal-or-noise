using SignalOrNoise.Core;

namespace SignalOrNoise.Ingestor;

public static class Deduper
{
    // A matching title only counts as a duplicate within this window. Syndicated copies appear within
    // hours; a daily recurring title ("Stocks making the biggest moves midday") is ~24h apart and
    // must stay a separate headline, so the window is well under a day.
    public static readonly TimeSpan TitleWindow = TimeSpan.FromHours(12);

    // Returns the incoming headlines that are new. Duplicates are not inserted; their ticker
    // is merged into the headline they duplicate (which may be an existing, tracked row).
    public static List<Headline> Merge(IEnumerable<Headline> existing, IEnumerable<Headline> incoming)
    {
        var known = existing.ToList();
        var added = new List<Headline>();
        foreach (var headline in incoming)
        {
            var match = known.FirstOrDefault(k => k.UrlHash == headline.UrlHash)
                ?? known.FirstOrDefault(k => k.TitleHash == headline.TitleHash
                    && (k.PublishedUtc - headline.PublishedUtc).Duration() <= TitleWindow);

            if (match is null)
            {
                known.Add(headline);
                added.Add(headline);
            }
            else
            {
                match.Tickers = MergeTickers(match.Tickers, headline.Tickers);
            }
        }
        return added;
    }

    public static string MergeTickers(string a, string b) =>
        string.Join(',', $"{a},{b}".Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .Order(StringComparer.Ordinal));
}
