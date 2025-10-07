using ai.lab.service.Services.Common;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ai.lab.service.HealthCheck;

public class OllamaHealthCheck(ILogger<OllamaHealthCheck> logger, IOllamaClient ollamaClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            var models = await ollamaClient.GetAvailableAiModels(cancellationToken);
            if (models.Any())
            {
                logger.LogInformation("Ollama service is healthy. Available models: {ModelCount}", models.Count);
                return HealthCheckResult.Healthy("Ollama service is reachable and has available models.");
            }
            else
            {
                logger.LogWarning("Ollama service is reachable but has no available models.");
                return HealthCheckResult.Degraded("Ollama service is reachable but has no available models.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ollama service health check failed: {ErrorMessage}", ex.Message);
            return HealthCheckResult.Unhealthy("Ollama service is unreachable or returned an error.");
        }
    }
}
