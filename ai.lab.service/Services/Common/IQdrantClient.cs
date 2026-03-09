using ai.lab.service.Model.Embeddings;

namespace ai.lab.service.Services.Common;

public interface IQdrantClient
{
    /// <summary>
    /// Asynchronously uploads a data chunk with the specified vector, file name, tags, and model identifier.
    /// </summary>
    /// <param name="chunkId">The unique identifier for the chunk to be uploaded. Cannot be null or empty.</param>
    /// <param name="vector">The vector data representing the chunk. Cannot be null.</param>
    /// <param name="fileName">The name of the file associated with the chunk. Cannot be null or empty.</param>
    /// <param name="tags">A list of tags to associate with the chunk. Can be null or empty if no tags are required.</param>
    /// <param name="model">The identifier of the model to associate with the chunk. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous upload operation.</returns>
    Task UploadChunkAsync(string chunkId, float[] vector, string fileName, List<string> tags, string model, CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronously uploads a batch of chunks to Qdrant in one operation.
    /// </summary>
    /// <param name="model">The model identifier used to resolve the collection.</param>
    /// <param name="chunks">The chunks to upload.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous upload operation.</returns>
    Task UploadChunksAsync(string model, List<QdrantChunkUpload> chunks, CancellationToken cancellationToken);

    /// <summary>
    /// Performs an asynchronous vector similarity search using the specified model and returns the top matching
    /// results.
    /// </summary>
    /// <param name="vector">The input vector to search for similar items. Must not be null.</param>
    /// <param name="topK">The maximum number of top results to return. Must be greater than zero.</param>
    /// <param name="model">The name of the model to use for the search. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a QdrantSearchResponse with the
    /// search results.</returns>
    Task<QdrantSearchResponse> QdrantSearchResponseAsync(float[] vector, int topK, string model, CancellationToken cancellationToken);
}
