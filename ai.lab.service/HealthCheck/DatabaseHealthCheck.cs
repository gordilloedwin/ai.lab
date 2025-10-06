using ai.lab.service.Options;
using ai.lab.service.Services.Common;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace ai.lab.service.HealthCheck;

public class DatabaseHealthCheck
(
    ILogger<DatabaseHealthCheck> logger,
    IDatabaseService databaseService,
    IMemoryCache memoryCache,
    IOptionsMonitor<DatabaseOptions> eventApiOptions
) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            if (!memoryCache.TryGetValue("MasterDbHealthCheck", out string? _))
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                await databaseService.TestMasterDataAccessAsync(cancellationToken);
                stopwatch.Stop();
                if (stopwatch.ElapsedMilliseconds >= (eventApiOptions?.CurrentValue?.MasterDataAccessHealthyTimeoutSeconds ?? 60) * 1000)
                {
                    logger.LogWarning(
                        "Database access took too long: {ElapsedMilliseconds} ms. " +
                        "Entering degraded state for this node [XRS.Reporting.DriverLogService]", stopwatch.ElapsedMilliseconds);
                    return HealthCheckResult.Degraded("xrs_master ping slow!");
                }

                logger.LogInformation("Database access took {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
                memoryCache.Set("MasterDbHealthCheck", "Healthy",
                    new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10), Size = 1, Priority = CacheItemPriority.Low });
            }

            return HealthCheckResult.Healthy("xrs_master ping successful!");
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "HealthCheck Database Access failure: {ErrorMessage}", e.Message);
            return HealthCheckResult.Unhealthy("xrs_master ping failed!");
        }
    }
}
