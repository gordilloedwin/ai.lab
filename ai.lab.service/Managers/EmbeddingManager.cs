using ai.lab.service.Model.Embeddings;
using ai.lab.service.Services.Common;

namespace ai.lab.service.Managers;

public class EmbeddingManager
(
    IOllamaClient ollamaClient,
    IQdrantClient qdrantClient,
    ILogger<EmbeddingManager> logger
) : IEmbeddingManager
{
    public async Task<QdrantSearchResponse> SearchChunksAsync(string model, string prompt, int topK = 5, CancellationToken cancellationToken = default)
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

            return await qdrantClient.QdrantSearchResponseAsync(queryVector, topK, cancellationToken);
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
