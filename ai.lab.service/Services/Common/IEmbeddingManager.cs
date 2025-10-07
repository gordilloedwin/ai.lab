namespace ai.lab.service.Services.Common;

public interface IEmbeddingManager
{
    /// <summary>
    /// Asynchronously searches for relevant text chunks using the specified model and prompt, returning up to the
    /// specified number of top results.
    /// </summary>
    /// <param name="model">The name or identifier of the model to use for searching. Cannot be null or empty.</param>
    /// <param name="prompt">The input prompt or query used to find relevant chunks. Cannot be null or empty.</param>
    /// <param name="topK">The maximum number of top matching chunks to return. Must be greater than zero. The default is 5.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the search operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of strings representing the
    /// top matching chunks. The list will be empty if no relevant chunks are found.</returns>
    Task<List<string>> SearchChunksAsync(string model, string prompt, int topK = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously uploads a text chunk to the specified model, associating it with a unique chunk identifier, file
    /// name, and optional tags.
    /// </summary>
    /// <remarks>If the upload is cancelled via the cancellation token, the operation will terminate without
    /// uploading the chunk. The chunkId must be unique within the context of the specified model to avoid
    /// conflicts.</remarks>
    /// <param name="model">The name of the model to which the chunk will be uploaded. Cannot be null or empty.</param>
    /// <param name="chunkId">A unique identifier for the chunk within the model. Cannot be null or empty.</param>
    /// <param name="chunkText">The text content of the chunk to upload. Cannot be null.</param>
    /// <param name="fileName">The name of the file associated with the chunk. Used for reference or grouping. Cannot be null or empty.</param>
    /// <param name="tags">A list of tags to associate with the chunk for categorization or metadata. Can be null or empty if no tags are
    /// required.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the upload operation.</param>
    /// <returns>A task that represents the asynchronous upload operation.</returns>
    Task UploadChunkAsync(string model, string chunkId, string chunkText, string fileName, List<string> tags, CancellationToken cancellationToken = default);
}