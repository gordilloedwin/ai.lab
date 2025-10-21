using ai.lab.service.Model.Database;
using ai.lab.service.Model.Embeddings;
using ai.lab.service.Options;
using ai.lab.service.Services.Common;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace ai.lab.service.Managers;

public class EmbeddingManager
(
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

    public async Task SaveChunkAsync(string model, string chunkId, string chunkText, string filePath, List<string> tags, CancellationToken cancellationToken = default)
    {
        try
        {
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
                    Tags = string.Join(",", tags),
                    Model = model,
                    Embedding = vector
                };

                await databaseService.InsertChunkAsync(chunks, cancellationToken);
            }

            logger.LogInformation("Uploaded chunk {ChunkId} successfully", chunkId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading chunk {ChunkId}", chunkId);
        }
    }
}
