using ai.lab.service.Model.Semantics;
using ai.lab.service.Services.Common;

namespace ai.lab.service.Services;

public class SemanticsService(ILogger<SemanticsService> logger) : ISemanticsService
{
    private readonly HttpClient _httpClient = new HttpClient();
    private const string OllamaUrl = "http://localhost:11434/api/embeddings";
    private const string QdrantUrl = "http://localhost:6333/collections/code_chunks/points";

    public async Task UploadChunkAsync(string chunkId, string chunkText, string fileName, List<string> tags)
    {
        // Step 1: Embed with Ollama
        var embeddingRequest = new
        {
            model = "llama3",
            prompt = chunkText
        };

        var ollamaResponse = await _httpClient.PostAsJsonAsync(OllamaUrl, embeddingRequest);
        ollamaResponse.EnsureSuccessStatusCode();

        var embeddingResult = await ollamaResponse.Content.ReadFromJsonAsync<EmbeddingResponse>();
        var vector = embeddingResult?.embedding;

        if (vector == null || vector.Length == 0)
        {            
            logger.LogError("Failed to get embedding for chunk {ChunkId}", chunkId);
            return;
        }

        // Step 2: Upload to Qdrant
        var qdrantRequest = new
        {
            points = new[]
            {
                new
                {
                    id = chunkId,
                    vector = vector,
                    payload = new
                    {
                        text = chunkText,
                        file = fileName,
                        tags = tags
                    }
                }
            }
        };

        var qdrantResponse = await _httpClient.PostAsJsonAsync(QdrantUrl, qdrantRequest);
        qdrantResponse.EnsureSuccessStatusCode();
        logger.LogInformation("Successfully uploaded chunk {ChunkId} to Qdrant", chunkId);
    }
}
