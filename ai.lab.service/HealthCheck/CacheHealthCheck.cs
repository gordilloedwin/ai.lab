using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ai.lab.service.HealthCheck;

public class CacheHealthCheck(ILogger<CacheHealthCheck> logger, IMemoryCache memoryCache) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            memoryCache.Set("HealthCheck", "OK",
                new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10), Size = 1, Priority = CacheItemPriority.Low });
            memoryCache.TryGetValue("HealthCheck", out string? _);
            memoryCache.Remove("HealthCheck");
            stopwatch.Stop();
            return Task.FromResult(stopwatch.ElapsedMilliseconds < 2000 ?
                HealthCheckResult.Healthy("in memory cache OK!") :
                HealthCheckResult.Degraded("in memory cache slow!"));
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "HealthCheck Memory Cache failure: {ErrorMessage}", e.Message);
            return Task.FromResult(HealthCheckResult.Unhealthy("memory cache failed!"));
        }
    }
}
