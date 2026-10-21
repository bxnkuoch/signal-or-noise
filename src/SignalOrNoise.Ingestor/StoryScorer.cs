using System.Text;
using System.Text.Json;
using SignalOrNoise.Core;

namespace SignalOrNoise.Ingestor;

public record StoryVerdict(
    string EventType, string PrimaryTicker, int Materiality, int Direction, string OneLineSummary, string Rationale);

public record ScoreResult(StoryVerdict Verdict, int Attempts, int InputTokens, int OutputTokens, decimal CostUsd, TimeSpan Latency);

public class ScoringException(string message) : Exception(message);

public class StoryScorer
{
    // Bump when the prompt, schema, or effort changes, so old and new scores are never mixed up.
    public const string PromptVersion = "v1";
    public const int MaxAttempts = 3;
    public const string NoTicker = "NONE";

    public static readonly string[] EventTypes =
    [
        "earnings", "guidance", "analyst_rating", "m_and_a", "legal_regulatory", "product",
        "management", "capital_return", "macro", "opinion", "other",
    ];

    public const string SystemPrompt = """
        You rate financial news for a trading desk. You get the headlines published so far about one news story, each with its publish time, and you judge how much the underlying news should matter for the stock price.

        Rules:
        - Use only the headlines given. Judge the news as of the latest headline's time; do not use knowledge of anything that happened afterward.
        - Some headlines only report the stock's own price move ("shares jump 8%"). Do not treat the move itself as news: judge the event behind it, and if no event is described, rate it low.
        - Opinion pieces, listicles, and stock-picking articles ("3 stocks to buy now", "Is X a buy?") carry little new information: use event_type "opinion" and materiality 0-2.
        - Feed ticker tags can be wrong. primary_ticker is the listed company the story is mainly about, judged from the text, or "NONE" if it is not mainly about one of them.

        materiality (integer 0-10): how much this should move the primary company's stock, or the overall market if primary_ticker is "NONE".
        - 0: no new information
        - 1-3: minor (routine analyst note, small product update, conference appearance)
        - 4-6: notable (rating change from a major bank, mid-size deal, regulatory step, small guidance change)
        - 7-8: major (earnings surprise, guidance cut or raise, big acquisition, CEO change)
        - 9-10: extraordinary (being acquired, bankruptcy, fraud, failed pivotal trial)

        direction: 1 if the news is good for the stock, -1 if bad, 0 if mixed or unclear.
        one_line_summary: one plain sentence, at most 25 words, saying what happened.
        rationale: one or two sentences explaining the materiality score.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private readonly ILlmClient _llm;
    private readonly ScoringOptions _options;
    private readonly HashSet<string> _tickers;
    private readonly Dictionary<string, JsonElement> _schema;
    private readonly ILogger<StoryScorer> _logger;

    public StoryScorer(ILlmClient llm, ScoringOptions options, IEnumerable<string> tickers, ILogger<StoryScorer> logger)
    {
        _llm = llm;
        _options = options;
        var allowed = tickers.Append(NoTicker).ToList();
        _tickers = [.. allowed];
        _schema = BuildSchema(allowed);
        _logger = logger;
    }

    public string Model => _llm.Model;

    // The no-lookahead rule: only headlines published at or before the cutoff, in a stable order.
    public static List<Headline> SelectInput(IEnumerable<Headline> headlines, DateTime scoredAsOfUtc) =>
        headlines.Where(h => h.PublishedUtc <= scoredAsOfUtc)
            .OrderBy(h => h.PublishedUtc)
            .ThenBy(h => h.Title, StringComparer.Ordinal)
            .ToList();

    public static string BuildUserMessage(IEnumerable<Headline> input)
    {
        var message = new StringBuilder("Headlines about one story, oldest first:\n");
        foreach (var h in input)
        {
            message.Append($"\n- {h.PublishedUtc:yyyy-MM-dd HH:mm} UTC | {h.Title}");
            if (h.Tickers.Length > 0) message.Append($" [feed tags: {h.Tickers}]");
            if (!string.IsNullOrEmpty(h.Summary)) message.Append($"\n  Summary: {Truncate(h.Summary, 300)}");
        }
        return message.ToString();
    }

    public string InputHash(IReadOnlyList<Headline> input) =>
        HeadlineNormalizer.Hash($"{_llm.Model}\n{PromptVersion}\n{SystemPrompt}\n{BuildUserMessage(input)}");

    public async Task<ScoreResult> ScoreAsync(IReadOnlyList<Headline> input, CancellationToken ct)
    {
        var request = new LlmRequest(SystemPrompt, BuildUserMessage(input), _schema);
        int inputTokens = 0, outputTokens = 0;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var result = await _llm.CompleteJsonAsync(request, ct);
            inputTokens += result.InputTokens;
            outputTokens += result.OutputTokens;
            if (result.Refused) throw new ScoringException("The model declined to score this story.");

            var error = Validate(result.Text, out var verdict);
            if (error is null)
                return new ScoreResult(verdict!, attempt, inputTokens, outputTokens, Cost(inputTokens, outputTokens), result.Latency);

            _logger.LogWarning("Score attempt {Attempt} was invalid: {Error}", attempt, error);
        }
        throw new ScoringException($"No valid score after {MaxAttempts} attempts.");
    }

    // Structured output guarantees the JSON shape; this checks what the schema can't (number ranges, lengths).
    // Returns null if valid, otherwise what is wrong.
    public string? Validate(string json, out StoryVerdict? verdict)
    {
        verdict = null;
        try
        {
            verdict = JsonSerializer.Deserialize<StoryVerdict>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            return $"not valid JSON: {ex.Message}";
        }

        if (verdict is null) return "empty response";
        if (!EventTypes.Contains(verdict.EventType)) return $"unknown event_type '{verdict.EventType}'";
        if (verdict.PrimaryTicker is null || !_tickers.Contains(verdict.PrimaryTicker))
            return $"unknown primary_ticker '{verdict.PrimaryTicker}'";
        if (verdict.Materiality is < 0 or > 10) return $"materiality {verdict.Materiality} is outside 0-10";
        if (verdict.Direction is < -1 or > 1) return $"direction {verdict.Direction} is not -1, 0, or 1";
        if (string.IsNullOrWhiteSpace(verdict.OneLineSummary) || verdict.OneLineSummary.Length > 300)
            return "one_line_summary is empty or longer than 300 characters";
        if (string.IsNullOrWhiteSpace(verdict.Rationale)) return "rationale is empty";
        return null;
    }

    public decimal Cost(int inputTokens, int outputTokens) =>
        (inputTokens * _options.InputUsdPerMTok + outputTokens * _options.OutputUsdPerMTok) / 1_000_000m;

    private static Dictionary<string, JsonElement> BuildSchema(IEnumerable<string> tickers) => new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
        ["required"] = JsonSerializer.SerializeToElement(new[]
            { "event_type", "primary_ticker", "materiality", "direction", "one_line_summary", "rationale" }),
        ["properties"] = JsonSerializer.SerializeToElement(new Dictionary<string, object>
        {
            ["event_type"] = new { type = "string", @enum = EventTypes },
            ["primary_ticker"] = new { type = "string", @enum = tickers.ToArray() },
            ["materiality"] = new { type = "integer" },
            ["direction"] = new { type = "integer", @enum = new[] { -1, 0, 1 } },
            ["one_line_summary"] = new { type = "string" },
            ["rationale"] = new { type = "string" },
        }),
    };

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max] + "…";
}
