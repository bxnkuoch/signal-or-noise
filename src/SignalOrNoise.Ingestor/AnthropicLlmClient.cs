using System.Diagnostics;
using Anthropic;
using Anthropic.Models.Messages;

namespace SignalOrNoise.Ingestor;

// Calls Claude with structured output, so the response text is JSON matching the request's schema.
// The SDK itself retries 429s, 5xx errors, and connection failures.
public class AnthropicLlmClient(AnthropicClient client, string model) : ILlmClient
{
    public string Model => model;

    public async Task<LlmResult> CompleteJsonAsync(LlmRequest request, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = model,
            MaxTokens = 16000,
            System = request.System,
            Messages = [new() { Role = Role.User, Content = request.User }],
            // Effort is part of what the prompt version pins: bump StoryScorer.PromptVersion if it changes.
            OutputConfig = new OutputConfig
            {
                Effort = Effort.Medium,
                Format = new JsonOutputFormat { Schema = request.Schema },
            },
        }, ct);

        var text = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
        return new LlmResult(text, response.StopReason == "refusal",
            (int)response.Usage.InputTokens, (int)response.Usage.OutputTokens, Stopwatch.GetElapsedTime(started));
    }
}
