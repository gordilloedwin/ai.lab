using ai.lab.service.Managers.Common;
using ai.lab.service.Model.Embeddings;
using ai.lab.service.Options;
using ai.lab.service.Services.Common;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ai.lab.service.Managers;

public class QdrantClientManager
(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<AILabOptions> options,
    ILogger<QdrantClientManager> logger
) : AILabBaseClient(httpClientFactory), IQdrantClient
{
    public static string ClientName => "QdrantClient";
    
    protected override string HttpClientName => ClientName;

    public async Task UploadChunkAsync(string chunkId, float[] vector, string fileName, List<string> tags, string model, CancellationToken cancellationToken)
    {
        try
        {
            var qdrantUrl = options.CurrentValue.QdrantUrl;
            var collectionName = string.IsNullOrWhiteSpace(model) ? options.CurrentValue.QdrantCollectionName : model;

            var payload = new
            {
                id = chunkId,
                vector = vector,
                payload = new
                {
                    text = chunkId,
                    fileName = fileName,
                    tags = tags
                }
            };

            var response = await HttpClient.PutAsJsonAsync($"{qdrantUrl}/collections/{collectionName}/points", payload, cancellationToken);
            response.EnsureSuccessStatusCode();
            logger.LogInformation("Uploaded chunk {ChunkId} to Qdrant", chunkId);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error uploading chunk {ChunkId} to Qdrant: {Message}", chunkId, ex.Message);
            throw new InvalidOperationException($"Failed to connect to Qdrant service: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || ex.CancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Timeout uploading chunk {ChunkId} to Qdrant: {Message}", chunkId, ex.Message);
            throw new TimeoutException("Qdrant API request timed out", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error uploading chunk {ChunkId} to Qdrant: {Message}", chunkId, ex.Message);
            throw new InvalidOperationException($"Unexpected error calling Qdrant service: {ex.Message}", ex);
        }
    }

    public async Task<QdrantSearchResponse> QdrantSearchResponseAsync(float[] vector, int topK, string model, CancellationToken cancellationToken)
    {
        try
            {
            var qdrantUrl = options.CurrentValue.QdrantUrl;
            var collectionName = string.IsNullOrWhiteSpace(model) ? options.CurrentValue.QdrantCollectionName : model;

            var searchRequest = new
            {
                vector = vector,
                top = topK,
                with_payload = true
            };

            var responseTask = await HttpClient.PostAsJsonAsync($"{qdrantUrl}/collections/{collectionName}/points/search", searchRequest, cancellationToken);
            var searchResults = await responseTask.Content.ReadFromJsonAsync<QdrantSearchResponse>(cancellationToken: cancellationToken);
            return searchResults ?? new QdrantSearchResponse();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error searching Qdrant: {Message}", ex.Message);
            throw new InvalidOperationException($"Failed to connect to Ollama service: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || ex.CancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Timeout searching Qdrant: {Message}", ex.Message);
            throw new TimeoutException("Ollama API request timed out", ex);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex.Message);
            throw new InvalidOperationException($"Invalid JSON response from Qdrant service: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error searching Qdrant: {Message}", ex.Message);
            throw new InvalidOperationException($"Unexpected error calling Qdrant service: {ex.Message}", ex);
        }
    }
}
