using System.Net;
using System.Security.Cryptography;
using System.Text;
using SignalOrNoise.Core;

namespace SignalOrNoise.Ingestor;

public static class HeadlineNormalizer
{
    public static Headline ToHeadline(FeedItem item, string source, string? ticker, DateTime ingestedUtc) => new()
    {
        Source = source,
        Url = item.Url,
        UrlHash = Hash(NormalizeUrl(item.Url)),
        Title = item.Title,
        TitleHash = Hash(NormalizeTitle(item.Title)),
        Summary = item.Summary,
        PublishedUtc = item.PublishedUtc,
        IngestedUtc = ingestedUtc,
        Tickers = ticker ?? "",
    };

    // Same article, different URL spelling: http vs https, host case, fragment, trailing slash, tracking params.
    public static string NormalizeUrl(string url)
    {
        var uri = new Uri(url.Trim());
        var query = string.Join('&', uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(param => !IsTrackingParam(param.Split('=')[0])));
        var port = uri.IsDefaultPort ? "" : $":{uri.Port}";
        return $"https://{uri.Host}{port}{uri.AbsolutePath.TrimEnd('/')}{(query.Length > 0 ? "?" + query : "")}";
    }

    // Lowercase, letters and digits only, single spaces: "Apple’s $1,999 Phone!" and "apple's $1,999 phone" match.
    public static string NormalizeTitle(string title)
    {
        var chars = WebUtility.HtmlDecode(title).ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : ' ')
            .ToArray();
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool IsTrackingParam(string key) =>
        key == ".tsrc" || key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase);
}
