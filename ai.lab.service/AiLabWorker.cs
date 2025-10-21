using ai.lab.ragfeed;
using ai.lab.service.Options;
using ai.lab.service.Services.Common;
using Microsoft.Extensions.Options;

namespace ai.lab.service;

public class AiLabWorker
(
    ILogger<AiLabWorker> logger,
    IEmbeddingManager embeddingManager,
    IOptionsMonitor<AILabOptions> optionsMonitor) : BackgroundService
{
    private Queue<string> folderQueue = new Queue<string>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (folderQueue.Count == 0)
            {
                logger.LogInformation("Folder queue is empty, checking for new repositories.");

                if (string.IsNullOrWhiteSpace(optionsMonitor.CurrentValue?.RepositoriesPath ?? string.Empty))
                {
                    logger.LogWarning("RepositoriesPath is not configured. Worker is idle.");
                    await Task.Delay(10000, stoppingToken);
                    continue;
                }

                var folders = Directory.GetDirectories(optionsMonitor.CurrentValue!.RepositoriesPath);
                if (folders.Length == 0)
                {
                    logger.LogInformation("No repositories found in {path}.", optionsMonitor.CurrentValue.RepositoriesPath);
                    await Task.Delay(10000, stoppingToken);
                    continue;
                }

                logger.LogInformation("Found {count} repositories in {path}.", folders.Length, optionsMonitor.CurrentValue.RepositoriesPath);

                foreach (var folder in folders)
                {
                    folderQueue.Enqueue(folder);
                }
            }

            if (folderQueue.TryDequeue(out var currentFolder))
            {
                logger.LogInformation("Processing folder: {folder}", currentFolder);
                
                if (Directory.Exists(currentFolder))
                {
                    foreach (var file in Directory.GetFiles(currentFolder, "*.*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var embeddings = new FileChunkGenerator().GenerateChunks(file);
                            await embeddingManager.SaveEmbeddingsAsync(embeddings, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error processing file: {file}", file);
                        }
                    }
                }
                else
                {
                    logger.LogWarning("Folder does not exist: {folder}", currentFolder);
                }

                await Task.Delay(5000, stoppingToken);
                logger.LogInformation("Finished processing folder: {folder}", currentFolder);
            }

            await Task.Delay(100000, stoppingToken);
        }
    }
}
