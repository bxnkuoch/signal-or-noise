using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SignalOrNoise.Ingestor;

public static partial class RssParser
{
    // Items without a title, link, or parseable pubDate are skipped: the eval needs a timestamp for every headline.
    public static List<FeedItem> Parse(string xml)
    {
        var items = new List<FeedItem>();
        foreach (var item in XDocument.Parse(xml).Descendants("item"))
        {
            var title = item.Element("title")?.Value.Trim();
            var link = item.Element("link")?.Value.Trim();
            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(link)) continue;
            if (!DateTimeOffset.TryParse(item.Element("pubDate")?.Value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var published)) continue;

            items.Add(new FeedItem(WebUtility.HtmlDecode(title), link, CleanSummary(item.Element("description")?.Value),
                published.UtcDateTime));
        }
        return items;
    }

    private static string? CleanSummary(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;
        var text = WebUtility.HtmlDecode(HtmlTag().Replace(description, " "));
        return Whitespace().Replace(text, " ").Trim();
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
