using ai.lab.service.Model.Embeddings;

namespace ai.lab.service.Services.Common;

public interface IQdrantClient
{
    /// <summary>
    /// Asynchronously uploads a vector chunk to the server, associating it with the specified file and tags.
    /// </summary>
    /// <remarks>If the operation is canceled via the cancellation token, the upload will not complete. This
    /// method does not return until the chunk has been successfully uploaded or the operation is canceled.</remarks>
    /// <param name="chunkId">The unique identifier for the chunk to upload. Cannot be null or empty.</param>
    /// <param name="vector">The vector data to upload as a chunk. Cannot be null. The array must contain at least one element.</param>
    /// <param name="fileName">The name of the file to associate with the uploaded chunk. Cannot be null or empty.</param>
    /// <param name="tags">A list of tags to associate with the chunk. Can be null or empty if no tags are required.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the upload operation.</param>
    /// <returns>A task that represents the asynchronous upload operation.</returns>
    Task UploadChunkAsync(string chunkId, float[] vector, string fileName, List<string> tags, CancellationToken cancellationToken);

    /// <summary>
    /// Executes an asynchronous vector similarity search against the Qdrant database and returns the search results.
    /// </summary>
    /// <param name="vector">The vector to use as the query for similarity search. Must not be null and should match the dimensionality
    /// expected by the Qdrant collection.</param>
    /// <param name="topK">The maximum number of top matching results to return. Must be greater than zero.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The operation is canceled if the token is triggered.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="QdrantSearchResponse"/>
    /// with the search results matching the query vector.</returns>
    Task<QdrantSearchResponse> QdrantSearchResponseAsync(float[] vector, int topK, CancellationToken cancellationToken);
}
