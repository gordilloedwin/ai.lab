using Polly;
using Polly.Extensions.Http;

namespace ai.lab.service.Services.Common;

public interface IAILabHttpClient : IDisposable
{
    IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() => HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(5, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
            );

    IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() => HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, breakDelay) =>
                {
                    Console.WriteLine($"Circuit broken! Delay: {breakDelay.TotalSeconds}s");
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

    /*
     * var clientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var client = clientFactory.CreateClient("OllamaClient");
     */
}
