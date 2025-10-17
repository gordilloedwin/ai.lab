using Polly;
using Polly.Extensions.Http;

namespace ai.lab.service.Managers.Common;

public abstract class AILabBaseClient(IHttpClientFactory httpClientFactory) : IDisposable
{
    private bool disposedValue;

    private HttpClient? _httpClient;    

    public static string? HttpClientName { get; }

    public HttpClient HttpClient => _httpClient ??= httpClientFactory.CreateClient(HttpClientName ?? string.Empty);

    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() => HttpPolicyExtensions
        .HandleTransientHttpError().OrInner<System.TimeoutException>()
        .WaitAndRetryAsync(5, retryAttempt =>
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

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                if (_httpClient != null)
                {
                    ((IDisposable)_httpClient).Dispose();
                }
            }

            _httpClient = null;
            disposedValue = true;
        }
    }

    ~AILabBaseClient()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
