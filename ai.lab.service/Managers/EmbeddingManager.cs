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

            if (optionsMonitor?.CurrentValue?.SaveChunksToQadrant ?? false)
            {
                await qdrantClient.UploadChunkAsync(chunkId, vector, filePath, tags, model, cancellationToken);
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

            logger.LogInformation("Uploaded chunk {ChunkId} successfully", chunkId);
            await Task.Delay(3000, cancellationToken); // Small delay to avoid overwhelming services
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading chunk {ChunkId}", chunkId);
        }
    }
}
