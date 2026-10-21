using System.Text.Json;

namespace SignalOrNoise.Ingestor;

// The one seam between scoring logic and the Claude API, so tests can use a fake.
public interface ILlmClient
{
    string Model { get; }
    Task<LlmResult> CompleteJsonAsync(LlmRequest request, CancellationToken ct);
}

public record LlmRequest(string System, string User, Dictionary<string, JsonElement> Schema);

public record LlmResult(string Text, bool Refused, int InputTokens, int OutputTokens, TimeSpan Latency);
