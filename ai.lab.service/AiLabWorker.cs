namespace ai.lab.service;

public class AiLabWorker(ILogger<AiLabWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(100000, stoppingToken);
        }
    }
}
