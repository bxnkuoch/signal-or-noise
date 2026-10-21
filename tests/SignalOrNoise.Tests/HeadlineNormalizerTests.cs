using SignalOrNoise.Ingestor;

namespace SignalOrNoise.Tests;

public class HeadlineNormalizerTests
{
    [Theory]
    [InlineData("https://finance.yahoo.com/m/abc/apple.html?.tsrc=rss", "https://finance.yahoo.com/m/abc/apple.html")]
    [InlineData("http://Example.COM/a/b/?utm_source=x&id=5&utm_medium=rss#top", "https://example.com/a/b?id=5")]
    [InlineData("https://example.com:8443/a", "https://example.com:8443/a")]
    public void NormalizeUrl_removes_differences_that_do_not_change_the_article(string url, string expected)
    {
        Assert.Equal(expected, HeadlineNormalizer.NormalizeUrl(url));
    }

    [Fact]
    public void NormalizeUrl_keeps_query_params_that_identify_the_article()
    {
        Assert.NotEqual(
            HeadlineNormalizer.NormalizeUrl("https://example.com/story?id=1"),
            HeadlineNormalizer.NormalizeUrl("https://example.com/story?id=2"));
    }

    [Theory]
    [InlineData("Apple’s $1,999 Foldable  Phone!", "apple s 1 999 foldable phone")]
    [InlineData("apple's $1,999 foldable phone", "apple s 1 999 foldable phone")]
    [InlineData("AT&amp;T Beats Estimates", "at t beats estimates")]
    public void NormalizeTitle_ignores_case_punctuation_and_whitespace(string title, string expected)
    {
        Assert.Equal(expected, HeadlineNormalizer.NormalizeTitle(title));
    }

    [Fact]
    public void ToHeadline_gives_the_same_hashes_to_trivially_different_copies()
    {
        var now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        var a = HeadlineNormalizer.ToHeadline(
            new FeedItem("Apple Stock Downgraded", "https://finance.yahoo.com/x.html?.tsrc=rss", null, now), "yahoo", "AAPL", now);
        var b = HeadlineNormalizer.ToHeadline(
            new FeedItem("APPLE STOCK DOWNGRADED", "http://finance.yahoo.com/x.html", null, now), "yahoo", "AAPL", now);

        Assert.Equal(a.UrlHash, b.UrlHash);
        Assert.Equal(a.TitleHash, b.TitleHash);
        Assert.Equal(64, a.UrlHash.Length);
    }
}
