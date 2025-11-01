using ai.lab.ragfeed;
using ai.lab.service.Options;
using ai.lab.service.Services.Common;
using Microsoft.Extensions.Options;

namespace ai.lab.service;

public class AiLabWorker
(
    ILogger<AiLabWorker> logger,
    IServiceScopeFactory serviceScopeFactory,
    IOptionsMonitor<AILabOptions> optionsMonitor) : BackgroundService
{
    private Queue<string> folderQueue = new Queue<string>();

    // Directories and patterns to exclude from RAG ingestion
    private static readonly string[] ExcludedDirectories = 
    [
        "bin", "obj", ".git", ".vs", ".vscode", "node_modules", 
        "packages", ".idea", "dist", "build", "out", "target"
    ];

    private static readonly string[] ExcludedPatterns = 
    [
        "test", "tests", "unittest", "unittests", "__tests__", 
        ".test.", ".spec.", "test.", "spec."
    ];

    /// <summary>
    /// Determines if a file should be processed based on exclusion rules.
    /// Filters out build artifacts, hidden folders, test files, and generated code.
    /// </summary>
    private static bool ShouldProcessFile(string filePath)
    {
        var pathParts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fileName = Path.GetFileName(filePath);

        // Exclude files in specific directories (case-insensitive)
        foreach (var excludedDir in ExcludedDirectories)
        {
            if (pathParts.Any(part => part.Equals(excludedDir, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        // Exclude hidden files and folders (starting with .)
        if (pathParts.Any(part => part.StartsWith('.') && part.Length > 1))
        {
            return false;
        }

        // Exclude test-related files and folders (case-insensitive)
        foreach (var pattern in ExcludedPatterns)
        {
            if (filePath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
        var delay = TimeSpan.FromSeconds(optionsMonitor?.CurrentValue?.WorkerDelaySeconds ?? 30).Milliseconds;

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("RAG ingestion started.");

            if (!(optionsMonitor?.CurrentValue?.IsRagIngestionEnabled ?? false))
            {
                logger.LogInformation("RAG ingestion is disabled. Worker is idle.");
                await Task.Delay(delay, stoppingToken);
                continue;
            }

            if (folderQueue.Count == 0)
            {
                logger.LogInformation("Folder queue is empty, checking for new repositories.");

                if (string.IsNullOrWhiteSpace(optionsMonitor?.CurrentValue?.RepositoriesPath ?? string.Empty))
                {
                    logger.LogWarning("RepositoriesPath is not configured. Worker is idle.");
                    await Task.Delay(delay, stoppingToken);
                    continue;
                }

                var folders = Directory.GetDirectories(optionsMonitor?.CurrentValue!.RepositoriesPath ?? string.Empty);
                if (folders.Length == 0)
                {
                    logger.LogInformation("No repositories found in {path}.", optionsMonitor?.CurrentValue.RepositoriesPath);
                    await Task.Delay(delay, stoppingToken);
                    continue;
                }

                logger.LogInformation("Found {count} repositories in {path}.", folders.Length, optionsMonitor?.CurrentValue.RepositoriesPath);

                foreach (var folder in folders)
                {
                    folderQueue.Enqueue(folder);
                }
            }

            while (!stoppingToken.IsCancellationRequested && folderQueue.TryDequeue(out var currentFolder))
            {
                logger.LogInformation("Processing folder: {folder}", currentFolder);

                if (Directory.Exists(currentFolder))
                {
                    using var scope = serviceScopeFactory.CreateScope();
                    var chunkExtractor = scope.ServiceProvider.GetRequiredService<IChunkExtractor>();
                    var embeddingManager = scope.ServiceProvider.GetRequiredService<IEmbeddingManager>();

                    var allFiles = Directory.GetFiles(currentFolder, "*.*", SearchOption.AllDirectories);
                    var files = allFiles.Where(ShouldProcessFile).ToArray();

                    logger.LogInformation("Found {Total} files, processing {Filtered} after filtering (excluded {Excluded})",
                        allFiles.Length, files.Length, allFiles.Length - files.Length);

                    foreach (var file in files)
                    {
                        try
                        {
                            if (chunkExtractor.GenerateFileChunks(file, out var chunkEmbeddings))
                            {
                                await embeddingManager.SaveEmbeddingsAsync(chunkEmbeddings, stoppingToken);
                                await Task.Delay(1000, stoppingToken); // Small delay to avoid overwhelming services
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error processing file: {file}", file);
                        }
                    }
                    // Let 'using var scope' handle disposal. No need to set to null or call Dispose().
                    await Task.Delay(delay, stoppingToken);
                }
                else
                {
                    logger.LogWarning("Folder does not exist: {folder}", currentFolder);
                }

                logger.LogInformation("Finished processing folder: {folder}", currentFolder);
            }

            await Task.Delay(delay, stoppingToken);
        }

        logger.LogInformation("Worker stopping at: {time}", DateTimeOffset.Now);
    }
}
