using ai.lab.service.Services.Common;

namespace ai.lab.service.Services;

public class EmbeddingManager
(
    IOllamaClient ollamaClient,
    IQdrantClient qdrantClient,
    ILogger<EmbeddingManager> logger
) : IEmbeddingManager
{
    public async Task<List<string>> SearchChunksAsync(string model, string prompt, int topK = 5, CancellationToken cancellationToken = default)
    {
        try
        {
            var embeddingResult = await ollamaClient.GenerateEmbeddingResponseAsync(model, prompt, cancellationToken);
            var queryVector = embeddingResult?.embedding;

            if (queryVector == null || queryVector.Length == 0)
            {
                logger.LogError("Failed to get embedding for prompt");
                return new List<string>();
            }

            var qdrantSearchResponse = await qdrantClient.QdrantSearchResponseAsync(queryVector, topK, cancellationToken);

            var matchedChunks = qdrantSearchResponse?.result?
                .Select(r => r.payload != null && r.payload.TryGetValue("text", out var textObj) ? textObj?.ToString() : null)?
                .Where(text => !string.IsNullOrEmpty(text))?.Select(text => text!)?.ToList() ?? [];
            return matchedChunks;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching chunks for prompt");
            throw;
        }
    }

    public async Task UploadChunkAsync(string model, string chunkId, string chunkText, string fileName, List<string> tags, CancellationToken cancellationToken = default)
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

            await qdrantClient.UploadChunkAsync(chunkId, vector, fileName, tags, CancellationToken.None);
            logger.LogInformation("Uploaded chunk {ChunkId} successfully", chunkId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading chunk {ChunkId}", chunkId);
        }
    }
}
