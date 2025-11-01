using Polly;
using Polly.Extensions.Http;

namespace ai.lab.service.Managers.Common;

public abstract class AILabBaseClient(IHttpClientFactory httpClientFactory)
{
    private HttpClient? _httpClient;

    protected abstract string HttpClientName { get; }

    public HttpClient HttpClient => _httpClient ??= httpClientFactory.CreateClient(HttpClientName);

    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() => HttpPolicyExtensions
        .HandleTransientHttpError().OrInner<System.TimeoutException>()
        .WaitAndRetryAsync(3, retryAttempt =>
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
        );

    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() => HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, breakDelay) =>
                {
                    Console.WriteLine("Circuit broken due to: {Reason}. Breaking for {Delay} seconds.",
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString(), breakDelay.TotalSeconds);
                },
                onReset: () =>
                {
                    Console.WriteLine("Circuit reset.");
                },
                onHalfOpen: () =>
                {
                    Console.WriteLine("Circuit half-open. Testing...");
                }
            );
}
