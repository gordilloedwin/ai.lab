using ai.lab.ragfeed.Output;
using ai.lab.service.Helpers;
using ai.lab.service.Model.Database;
using ai.lab.service.Model.Embeddings;
using ai.lab.service.Options;
using ai.lab.service.Services.Common;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ai.lab.service.Managers;

public class EmbeddingManager
(
    IMemoryCache memoryCache,
    IOllamaClient ollamaClient,
    IQdrantClient qdrantClient,
    IDatabaseService databaseService,
    IOptionsMonitor<AILabOptions> optionsMonitor,
    ILogger<EmbeddingManager> logger
) : IEmbeddingManager
{
    public string GenerateChunkId(string model, string filePath, string chunkText)
    {
        string input = $"{model}:{filePath}:{chunkText}";
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            StringBuilder sb = new StringBuilder();
            foreach (byte b in hashBytes)
                sb.Append(b.ToString("x2")); // hex format
            return sb.ToString();
        }
    }

    public async Task<bool> DeleteOldChunksFromMariaDb(string filePath, CancellationToken cancellationToken) =>
        await databaseService.DeleteOldChunksAsync(filePath, cancellationToken);

    public async Task<QdrantSearchResponse> SearchChunksInQdrantAsync(string model, string prompt, int topK = 5, CancellationToken cancellationToken = default)
    {
        try
        {
            var embeddingResult = await ollamaClient.GenerateEmbeddingResponseAsync(model, prompt, cancellationToken);
            var queryVector = embeddingResult?.embedding;

            if (queryVector == null || queryVector.Length == 0)
            {
                logger.LogError("Failed to get embedding for prompt");
                return new QdrantSearchResponse();
            }

            return await qdrantClient.QdrantSearchResponseAsync(queryVector, topK, model, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching chunks for prompt");
            throw;
        }
    }

    public async Task<List<MariaDbChunkEmbedding>> GetRelevantEmbeddingsFromMariaDbAsync(string model, string prompt, int topK = 5, CancellationToken cancellationToken = default)
    {
        try
        {
            var embeddingResult = await ollamaClient.GenerateEmbeddingResponseAsync(model, prompt, cancellationToken);
            var queryVector = embeddingResult?.embedding;

            if (queryVector == null || queryVector.Length == 0)
            {
                logger.LogError("Failed to get embedding for prompt");
                return new List<MariaDbChunkEmbedding>();
            }

            // Extract relevant tags from the user's prompt using the cached semantic tags
            List<string>? filterTags = null;
            if (memoryCache.TryGetValue("semantic-tags", out List<string>? availableTags) && availableTags?.Count > 0)
            {
                var tagMatcher = new TagMatcher(availableTags);
                var matchedTags = tagMatcher.MatchTags(prompt);
                
                if (matchedTags.Count > 0)
                {
                    filterTags = matchedTags;
                    logger.LogInformation("Extracted {Count} tags from prompt: {Tags}", matchedTags.Count, string.Join(", ", matchedTags));
                }
                else
                {
                    logger.LogDebug("No matching tags found in prompt, using pure vector similarity search");
                }
            }
            else
            {
                logger.LogDebug("Semantic tags not available in cache, using pure vector similarity search");
            }

            return await databaseService.GetRelevantChunksAsync(model, queryVector, topK, filterTags, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving relevant embeddings from MariaDB for prompt");
            throw;
        }
    }

    public async Task SaveEmbeddingsAsync(List<ChunkEmbedding> chunkEmbeddings, CancellationToken cancellationToken = default)
    {
        if (!chunkEmbeddings.Any())
        {
            logger.LogWarning("No chunk embeddings provided to save.");
            return;
        }

        if (!memoryCache.TryGetValue("embedding-models", out List<string>? models) || models == null)
        {
            models = await ollamaClient.GetAvailableAiModels(cancellationToken);
            models = models.Where(m => m.ToLowerInvariant().Contains(optionsMonitor.CurrentValue.EmbeddingsModel)).ToList();
            memoryCache.Set("embedding-models", models, TimeSpan.FromHours(1));
        }

        if (!memoryCache.TryGetValue("semantic-tags", out List<string>? tags) || tags == null)
        {
            if (!File.Exists("semantic-tags.txt"))
            {
                throw new FileNotFoundException("The semantic-tags.txt file was not found in the working directory.");
            }

            var semanticTags = File.ReadAllText("semantic-tags.txt");
            var fileLines = semanticTags.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            memoryCache.Set("semantic-tags", fileLines?.Where(t => !t.StartsWith("#"))?.Distinct() ?? [], TimeSpan.FromHours(1));
            tags = fileLines?.Where(t => !t.StartsWith("#"))?.Distinct().ToList() ?? new List<string>();
        }

        var tagMatcher = new TagMatcher(tags ?? []);
        var filePath = chunkEmbeddings.First().FileName;
        if (optionsMonitor.CurrentValue.ForceUpdateEmbeddings)
        {
            await DeleteOldChunksFromMariaDb(filePath, cancellationToken);
        }                

        foreach (var model in models)
        {
            foreach (var chunk in chunkEmbeddings)
            {
                var fileNoMainPath = filePath.Remove(0, optionsMonitor.CurrentValue.RepositoriesPath.Length).TrimStart(Path.DirectorySeparatorChar);

                chunk.Model = model;
                chunk.ChunkId = GenerateChunkId(chunk.Model, chunk.FileName, chunk.ChunkText);
                chunk.Tags.AddRange(tagMatcher.MatchTags(chunk.ChunkText.ToLowerInvariant() + " " + fileNoMainPath.ToLowerInvariant()));
                chunk.Tags = chunk.Tags.Where(t => t.Length > 3).Distinct().ToList();
                await SaveChunkAsync(chunk.Model, chunk.ChunkId, chunk.ChunkText, chunk.FileName, chunk.Tags, cancellationToken);
            }
        }
    }

    public async Task<int> SyncFileChunksToQdrantFromMariaDbAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!(optionsMonitor?.CurrentValue?.SaveChunksToQadrant ?? false))
        {
            logger.LogDebug("Qdrant sync disabled. Skipping file {FilePath}", filePath);
            return 0;
        }

        var fileChunks = await databaseService.GetChunksByFileAsync(filePath, cancellationToken);
        if (fileChunks.Count == 0)
        {
            logger.LogInformation("No MariaDB chunks found for file {FilePath}. Skipping Qdrant sync.", filePath);
            return 0;
        }

        int uploadedCount = 0;
        var expectedDimension = optionsMonitor.CurrentValue.EmbeddingsDimension;
        var batchSize = Math.Max(1, optionsMonitor.CurrentValue.QdrantUploadBatchSize);
        var interBatchDelayMs = Math.Max(0, optionsMonitor.CurrentValue.QdrantUploadInterBatchDelayMs);

        foreach (var modelGroup in fileChunks.GroupBy(c => c.Model))
        {
            var model = modelGroup.Key;
            if (string.IsNullOrWhiteSpace(model))
            {
                continue;
            }

            var uploads = new List<QdrantChunkUpload>();

            foreach (var chunk in modelGroup)
            {
                if (chunk.Embedding == null || chunk.Embedding.Length != expectedDimension)
                {
                    logger.LogWarning(
                        "Skipping chunk {ChunkId} due to embedding dimension mismatch. Expected {Expected}, got {Actual}",
                        chunk.ChunkId,
                        expectedDimension,
                        chunk.Embedding?.Length ?? 0);
                    continue;
                }

                uploads.Add(new QdrantChunkUpload
                {
                    ChunkId = string.IsNullOrWhiteSpace(chunk.ChunkId) ? GenerateChunkId(model, filePath, chunk.ChunkText ?? string.Empty) : chunk.ChunkId,
                    Vector = chunk.Embedding,
                    FileName = chunk.FileName ?? filePath,
                    Content = chunk.ChunkText ?? string.Empty,
                    Tags = ParseTags(chunk.Tags)
                });
            }

            if (uploads.Count == 0)
            {
                continue;
            }

            for (int i = 0; i < uploads.Count; i += batchSize)
            {
                var currentBatch = uploads.Skip(i).Take(batchSize).ToList();
                await qdrantClient.UploadChunksAsync(model, currentBatch, cancellationToken);
                uploadedCount += currentBatch.Count;

                var hasMoreBatches = i + batchSize < uploads.Count;
                if (hasMoreBatches && interBatchDelayMs > 0)
                {
                    await Task.Delay(interBatchDelayMs, cancellationToken);
                }
            }

            logger.LogInformation(
                "Synced {Count} chunks to Qdrant for file {FilePath}, model {Model}, using batch size {BatchSize}, inter-batch delay {DelayMs}ms",
                uploads.Count,
                filePath,
                model,
                batchSize,
                interBatchDelayMs);
        }

        logger.LogInformation("Synced {Count} chunks from MariaDB to Qdrant for file {FilePath}", uploadedCount, filePath);
        return uploadedCount;
    }

    private static List<string> ParseTags(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
        {
            return [];
        }

        try
        {
            var tags = JsonSerializer.Deserialize<List<string>>(tagsJson);
            return tags?.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList() ?? [];
        }
        catch
        {
            return tagsJson
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();
        }
    }

    public async Task SaveChunkAsync(string model, string chunkId, string chunkText, string filePath, List<string> tags, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await databaseService.ValidateHashAlreadyProcessedAsync(chunkId, filePath, cancellationToken))
            {
                logger.LogInformation("Chunk {ChunkId} already processed, skipping upload", chunkId);
                return;
            }

            var embeddingResult = await ollamaClient.GenerateEmbeddingResponseAsync(model, chunkText, cancellationToken);
            var vector = embeddingResult?.embedding;

            if (vector == null || vector.Length == 0)
            {
                logger.LogError("Failed to get embedding for chunk {ChunkId}", chunkId);
                return;
            }

            // Validate vector dimension (MariaDB VECTOR column is defined as vector(4096))
            const int expectedDimension = 4096;
            if (vector.Length != expectedDimension)
            {
                logger.LogError("Embedding dimension mismatch for chunk {ChunkId}. Expected {Expected} but got {Actual}", 
                    chunkId, expectedDimension, vector.Length);
                return;
            }

            if (optionsMonitor?.CurrentValue?.SaveChunksToMariaDb ?? false)
            {
                var chunks = new MariaDbChunkEmbedding
                {
                    ChunkId = chunkId,
                    ChunkText = chunkText,
                    FileName = filePath,
                    Tags = System.Text.Json.JsonSerializer.Serialize(tags),
                    Model = model,
                    Embedding = vector
                };

                await databaseService.InsertChunkAsync(chunks, cancellationToken);
            }

            await Task.Delay(3000, cancellationToken); // Small delay to avoid overwhelming services
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading chunk {ChunkId}", chunkId);
        }
    }
}
