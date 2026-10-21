using SignalOrNoise.Ingestor;

namespace SignalOrNoise.Tests;

public class RssParserTests
{
    private const string Feed = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0"><channel><title>Test</title>
          <item>
            <title>AT&amp;T Beats Estimates</title>
            <link>https://example.com/att</link>
            <description>&lt;p&gt;Shares   &lt;b&gt;rose&lt;/b&gt; after hours.&lt;/p&gt;</description>
            <pubDate>Tue, 20 Oct 2026 21:05:36 +0000</pubDate>
          </item>
          <item>
            <title>Eastern time item</title>
            <link>https://example.com/est</link>
            <pubDate>Tue, 20 Oct 2026 17:00:00 -0400</pubDate>
          </item>
          <item>
            <title>GMT item</title>
            <link>https://example.com/gmt</link>
            <pubDate>Tue, 20 Oct 2026 22:03:15 GMT</pubDate>
          </item>
          <item>
            <title>No date, skipped</title>
            <link>https://example.com/nodate</link>
          </item>
          <item>
            <title>No link, skipped</title>
            <pubDate>Tue, 20 Oct 2026 22:03:15 GMT</pubDate>
          </item>
        </channel></rss>
        """;

    [Fact]
    public void Parses_items_and_skips_ones_without_a_link_or_date()
    {
        var items = RssParser.Parse(Feed);

        Assert.Equal(["AT&T Beats Estimates", "Eastern time item", "GMT item"], items.Select(i => i.Title));
        Assert.Equal("https://example.com/att", items[0].Url);
    }

    [Fact]
    public void Converts_every_pubDate_offset_to_utc()
    {
        var items = RssParser.Parse(Feed);

        Assert.All(items, i => Assert.Equal(DateTimeKind.Utc, i.PublishedUtc.Kind));
        Assert.Equal(new DateTime(2026, 10, 20, 21, 5, 36, DateTimeKind.Utc), items[0].PublishedUtc);
        Assert.Equal(new DateTime(2026, 10, 20, 21, 0, 0, DateTimeKind.Utc), items[1].PublishedUtc);
        Assert.Equal(new DateTime(2026, 10, 20, 22, 3, 15, DateTimeKind.Utc), items[2].PublishedUtc);
    }

    [Fact]
    public void Strips_html_from_the_summary()
    {
        var items = RssParser.Parse(Feed);

        Assert.Equal("Shares rose after hours.", items[0].Summary);
        Assert.Null(items[1].Summary);
    }
}
