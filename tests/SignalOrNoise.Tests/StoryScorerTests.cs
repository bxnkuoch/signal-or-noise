using Microsoft.Extensions.Logging.Abstractions;
using SignalOrNoise.Core;
using SignalOrNoise.Ingestor;

namespace SignalOrNoise.Tests;

public class StoryScorerTests
{
    private static readonly DateTime Noon = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private const string ValidJson = """
        {"event_type":"analyst_rating","primary_ticker":"AAPL","materiality":4,"direction":-1,
         "one_line_summary":"Loop Capital downgraded Apple on slow iPhone sales.","rationale":"A single broker downgrade."}
        """;

    // Returns canned responses in order and records every request it receives.
    private class FakeLlmClient(params LlmResult[] responses) : ILlmClient
    {
        private readonly Queue<LlmResult> _responses = new(responses);
        public List<LlmRequest> Requests { get; } = [];
        public string Model => "fake-model";

        public Task<LlmResult> CompleteJsonAsync(LlmRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private static LlmResult Reply(string text, bool refused = false) =>
        new(text, refused, InputTokens: 1000, OutputTokens: 200, Latency: TimeSpan.FromMilliseconds(800));

    private static StoryScorer Scorer(ILlmClient llm) => new(
        llm,
        new ScoringOptions { InputUsdPerMTok = 0.10m, OutputUsdPerMTok = 0.50m },
        ["AAPL", "MSFT"],
        NullLogger<StoryScorer>.Instance);

    private static Headline Make(string title, DateTime published) =>
        new() { Title = title, Tickers = "AAPL", PublishedUtc = published };

    private static readonly Headline[] Input = [Make("Loop Capital downgrades Apple", Noon)];

    [Fact]
    public async Task Valid_response_is_parsed_and_costed()
    {
        var llm = new FakeLlmClient(Reply(ValidJson));

        var result = await Scorer(llm).ScoreAsync(Input, CancellationToken.None);

        Assert.Equal(new StoryVerdict("analyst_rating", "AAPL", 4, -1,
            "Loop Capital downgraded Apple on slow iPhone sales.", "A single broker downgrade."), result.Verdict);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(0.0002m, result.CostUsd); // 1000 * $0.10/M + 200 * $0.50/M
    }

    [Fact]
    public async Task Invalid_response_is_retried_and_all_attempts_are_counted_in_the_cost()
    {
        var llm = new FakeLlmClient(Reply("{not json"), Reply(ValidJson));

        var result = await Scorer(llm).ScoreAsync(Input, CancellationToken.None);

        Assert.Equal(2, result.Attempts);
        Assert.Equal(2000, result.InputTokens);
        Assert.Equal(400, result.OutputTokens);
        Assert.Equal(2, llm.Requests.Count);
    }

    [Fact]
    public async Task Gives_up_after_three_invalid_responses()
    {
        var outOfRange = ValidJson.Replace("\"materiality\":4", "\"materiality\":15");
        var llm = new FakeLlmClient(Reply(outOfRange), Reply(outOfRange), Reply(outOfRange));

        await Assert.ThrowsAsync<ScoringException>(() => Scorer(llm).ScoreAsync(Input, CancellationToken.None));
        Assert.Equal(3, llm.Requests.Count);
    }

    [Fact]
    public async Task Refusal_is_not_retried()
    {
        var llm = new FakeLlmClient(Reply("", refused: true), Reply(ValidJson));

        await Assert.ThrowsAsync<ScoringException>(() => Scorer(llm).ScoreAsync(Input, CancellationToken.None));
        Assert.Single(llm.Requests);
    }

    [Theory]
    [InlineData("\"materiality\":4", "\"materiality\":-1", "materiality")]
    [InlineData("\"direction\":-1", "\"direction\":2", "direction")]
    [InlineData("\"primary_ticker\":\"AAPL\"", "\"primary_ticker\":\"TSLA\"", "primary_ticker")]
    [InlineData("\"event_type\":\"analyst_rating\"", "\"event_type\":\"rumor\"", "event_type")]
    public void Validate_rejects_values_the_schema_cannot_rule_out(string from, string to, string expectedField)
    {
        var error = Scorer(new FakeLlmClient()).Validate(ValidJson.Replace(from, to), out _);

        Assert.NotNull(error);
        Assert.Contains(expectedField, error);
    }

    [Fact]
    public void Validate_accepts_none_as_primary_ticker()
    {
        var json = ValidJson.Replace("\"primary_ticker\":\"AAPL\"", "\"primary_ticker\":\"NONE\"");

        Assert.Null(Scorer(new FakeLlmClient()).Validate(json, out _));
    }

    [Fact]
    public void SelectInput_excludes_headlines_published_after_the_cutoff()
    {
        var headlines = new[]
        {
            Make("Apple shares sink 6% after downgrade", Noon.AddMinutes(45)),
            Make("Loop Capital downgrades Apple", Noon),
            Make("Apple downgraded on iPhone fears", Noon.AddMinutes(30)),
        };

        var input = StoryScorer.SelectInput(headlines, Noon.AddMinutes(30));

        Assert.Equal(["Loop Capital downgrades Apple", "Apple downgraded on iPhone fears"], input.Select(h => h.Title));
    }

    [Fact]
    public void InputHash_changes_when_the_input_changes_and_not_otherwise()
    {
        var scorer = Scorer(new FakeLlmClient());
        var one = new[] { Make("Loop Capital downgrades Apple", Noon) };
        var two = new[] { Make("Loop Capital downgrades Apple", Noon), Make("Apple downgraded", Noon.AddMinutes(5)) };

        Assert.Equal(scorer.InputHash(one), scorer.InputHash([Make("Loop Capital downgrades Apple", Noon)]));
        Assert.NotEqual(scorer.InputHash(one), scorer.InputHash(two));
    }

    [Fact]
    public async Task Prompt_contains_each_headline_with_its_time_and_the_tickers_are_in_the_schema()
    {
        var llm = new FakeLlmClient(Reply(ValidJson));

        await Scorer(llm).ScoreAsync(Input, CancellationToken.None);

        var request = Assert.Single(llm.Requests);
        Assert.Contains("2026-10-20 12:00 UTC | Loop Capital downgrades Apple", request.User);
        var tickerEnum = request.Schema["properties"].GetProperty("primary_ticker").GetProperty("enum");
        Assert.Equal(["AAPL", "MSFT", "NONE"], tickerEnum.EnumerateArray().Select(e => e.GetString()));
    }
}
