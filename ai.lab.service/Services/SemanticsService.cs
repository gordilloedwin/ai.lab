using ai.lab.service.Model.Semantics;
using ai.lab.service.Services.Common;

namespace ai.lab.service.Services;

public class SemanticsService(ILogger<SemanticsService> logger) : ISemanticsService
{
    private readonly HttpClient _httpClient = new HttpClient();
    private const string OllamaUrl = "http://localhost:11434/api/embeddings";
    private const string QdrantUrl = "http://localhost:6333/collections/code_chunks/points";

    public async Task<List<string>> SearchChunksAsync(string prompt, int topK = 5)
    {
        // Step 1: Embed the prompt using Ollama
        var embeddingRequest = new
        {
            model = "llama3",
            prompt = prompt
        };

        var ollamaResponse = await _httpClient.PostAsJsonAsync(OllamaUrl, embeddingRequest);
        ollamaResponse.EnsureSuccessStatusCode();

        var embeddingResult = await ollamaResponse.Content.ReadFromJsonAsync<EmbeddingResponse>();
        var queryVector = embeddingResult?.embedding;

        if (queryVector == null || queryVector.Length == 0)
        {
            logger.LogError("Failed to get embedding for prompt");
            return new List<string>();
        }

        // Step 2: Search Qdrant
        var searchRequest = new
        {
            vector = queryVector,
            top = topK,
            with_payload = true
        };

        var qdrantSearchResponse = await _httpClient.PostAsJsonAsync($"{QdrantUrl}/collections/your_collection/points/search", searchRequest);
        qdrantSearchResponse.EnsureSuccessStatusCode();

        var searchResults = await qdrantSearchResponse.Content.ReadFromJsonAsync<QdrantSearchResponse>();

        // Step 3: Extract matched chunks
        var matchedChunks = searchResults?.result?
            .Select(r => r.payload?.text?.ToString())
            .Where(text => !string.IsNullOrEmpty(text))
            .ToList() ?? new List<string>();

        logger.LogInformation("Retrieved {Count} chunks for prompt", matchedChunks.Count);
        return matchedChunks;
    }

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
                        tags = tags ?? new List<string>()
                    }
                }
            }
        };

        var qdrantResponse = await _httpClient.PostAsJsonAsync(QdrantUrl, qdrantRequest);
        if (!qdrantResponse.IsSuccessStatusCode)
        {
            logger.LogError("Failed to upload chunk {ChunkId} to Qdrant. Status: {StatusCode}", chunkId, qdrantResponse.StatusCode);
            return;
        }

        logger.LogInformation("Successfully uploaded chunk {ChunkId} to Qdrant", chunkId);
    }
}
