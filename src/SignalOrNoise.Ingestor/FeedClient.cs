using System.Net;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace SignalOrNoise.Ingestor;

public class FeedClient(HttpClient http)
{
    // Retry transient failures (network errors, timeouts, 429, 5xx) with jittered exponential backoff.
    // The timeout applies per attempt, inside the retry.
    private static readonly ResiliencePipeline<HttpResponseMessage> Pipeline =
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests || (int)r.StatusCode >= 500),
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(2),
                OnRetry = args =>
                {
                    args.Outcome.Result?.Dispose();
                    return default;
                },
            })
            .AddTimeout(TimeSpan.FromSeconds(20))
            .Build();

    public async Task<string> GetAsync(string url, CancellationToken ct)
    {
        using var response = await Pipeline.ExecuteAsync(async token => await http.GetAsync(url, token), ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
