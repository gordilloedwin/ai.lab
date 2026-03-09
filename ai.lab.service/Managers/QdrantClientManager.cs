using ai.lab.service.Managers.Common;
using ai.lab.service.Model.Embeddings;
using ai.lab.service.Options;
using ai.lab.service.Services.Common;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using System.Text;

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

    private string GetCollectionName(string model)
    {
        var collectionName = string.IsNullOrWhiteSpace(model) ? options.CurrentValue.QdrantCollectionName : model;
        return collectionName.Replace(":", "_").Replace("-", "_").ToLowerInvariant();
    }

    private static (string Host, int Port) ParseQdrantEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("QdrantUrl is not configured.");
        }

        var normalized = endpoint.Trim();

        if (!normalized.Contains("://", StringComparison.Ordinal))
        {
            normalized = $"http://{normalized}";
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Invalid Qdrant URL format: {endpoint}");
        }

        if (string.IsNullOrWhiteSpace(uri.Host) || uri.Port <= 0)
        {
            throw new InvalidOperationException($"Invalid Qdrant URL format: {endpoint}");
        }

        return (uri.Host, uri.Port);
    }

    private async Task<QdrantClient> GetOrCreateClientWithCollectionAsync(string collectionName, CancellationToken cancellationToken)
    {
        var (host, port) = ParseQdrantEndpoint(options.CurrentValue.QdrantUrl);
        var client = new QdrantClient(host, port);
        var exists = await client.CollectionExistsAsync(collectionName, cancellationToken: cancellationToken);
        if (!exists)
        {
            await client.CreateCollectionAsync
            (
                collectionName,
                new VectorParams
                {
                    Size = (ulong)options.CurrentValue.EmbeddingsDimension,
                    Distance = Distance.Cosine
                },
                cancellationToken: cancellationToken
            );

            logger.LogInformation("Created Qdrant collection {CollectionName}", collectionName);
        }

        return client;
    }

    private static Guid CreatePointGuid(string chunkId)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(chunkId));
        return new Guid(bytes);
    }

    public async Task UploadChunkAsync(string chunkId, float[] vector, string fileName, List<string> tags, string model, CancellationToken cancellationToken)
    {
        var chunks = new List<QdrantChunkUpload>
        {
            new()
            {
                ChunkId = chunkId,
                Vector = vector,
                FileName = fileName,
                Tags = tags ?? [],
                Content = string.Empty
            }
        };

        await UploadChunksAsync(model, chunks, cancellationToken);
    }

    public async Task UploadChunksAsync(string model, List<QdrantChunkUpload> chunks, CancellationToken cancellationToken)
    {
        if (chunks == null || chunks.Count == 0)
        {
            logger.LogDebug("UploadChunksAsync called with empty chunk set for model {Model}", model);
            return;
        }

        try
        {
            var collectionName = GetCollectionName(model);
            using var client = await GetOrCreateClientWithCollectionAsync(collectionName, cancellationToken);

            var points = chunks
                .Where(c => c.Vector?.Length > 0)
                .Select(c => new PointStruct
                {
                    Id = CreatePointGuid(c.ChunkId),
                    Vectors = new Vectors { Vector = c.Vector },
                    Payload =
                    {
                        { "chunkId", c.ChunkId },
                        { "fileName", c.FileName },
                        { "tags", string.Join(",", c.Tags ?? Enumerable.Empty<string>()) },
                        { "content", c.Content }
                    }
                })
                .ToList();

            if (points.Count == 0)
            {
                logger.LogWarning("No valid points to upload for model {Model}", model);
                return;
            }

            var upsertResult = await client.UpsertAsync(collectionName, points, cancellationToken: cancellationToken);
            logger.LogInformation("Uploaded {Count} chunks to Qdrant collection {CollectionName}. Result: {Result}", points.Count, collectionName, upsertResult);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error uploading chunks to Qdrant: {Message}", ex.Message);
            throw new InvalidOperationException($"Failed to connect to Qdrant service: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || ex.CancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Timeout uploading chunks to Qdrant: {Message}", ex.Message);
            throw new TimeoutException("Qdrant API request timed out", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error uploading chunks to Qdrant: {Message}", ex.Message);
            throw new InvalidOperationException($"Unexpected error calling Qdrant service: {ex.Message}", ex);
        }
    }

    public async Task<QdrantSearchResponse> QdrantSearchResponseAsync(float[] vector, int topK, string model, CancellationToken cancellationToken)
    {
        try
        {
            var collectionName = GetCollectionName(model);
            using var client = await GetOrCreateClientWithCollectionAsync(collectionName, cancellationToken);

            var searchResults = await client.SearchAsync(
                collectionName,
                vector,
                limit: (ulong)Math.Max(1, topK),
                payloadSelector: true,
                cancellationToken: cancellationToken);

            var response = new QdrantSearchResponse
            {
                result = searchResults
                    .Select(r => new SearchResult
                    {
                        payload = r.Payload.ToDictionary(
                            kvp => kvp.Key,
                            kvp => (object)(kvp.Value.StringValue ?? kvp.Value.ToString()))
                    })
                    .ToList()
            };

            return response;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error searching Qdrant: {Message}", ex.Message);
            throw new InvalidOperationException($"Failed to connect to Qdrant service: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || ex.CancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Timeout searching Qdrant: {Message}", ex.Message);
            throw new TimeoutException("Qdrant API request timed out", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error searching Qdrant: {Message}", ex.Message);
            throw new InvalidOperationException($"Unexpected error calling Qdrant service: {ex.Message}", ex);
        }
    }
}
