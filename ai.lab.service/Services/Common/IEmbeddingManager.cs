using ai.lab.service.Model.Embeddings;

namespace ai.lab.service.Services.Common;

public interface IEmbeddingManager
{
    /// <summary>
    /// Generates a unique identifier for a specific chunk of text within a file.
    /// </summary>
    /// <param name="model">The name or identifier of the model associated with the chunk. Must not be null or empty.</param>
    /// <param name="filePath">The path to the file containing the chunk. Must not be null or empty.</param>
    /// <param name="chunkText">The text content of the chunk for which to generate an identifier. Must not be null.</param>
    /// <returns>A string representing the unique identifier for the specified chunk of text.</returns>
    string GenerateChunkId(string model, string filePath, string chunkText);

    /// <summary>
    /// Deletes old data chunks associated with the specified file from the MariaDB database asynchronously.
    /// </summary>
    /// <param name="filePath">The full path of the file whose old data chunks should be deleted. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if any old chunks
    /// were deleted; otherwise, <see langword="false"/>.</returns>
    Task<bool> DeleteOldChunksFromMariaDb(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a semantic search for relevant chunks using the specified model and prompt.
    /// </summary>
    /// <param name="model">The name of the model to use for the semantic search. Cannot be null or empty.</param>
    /// <param name="prompt">The input prompt or query to search for relevant chunks. Cannot be null or empty.</param>
    /// <param name="topK">The maximum number of top matching chunks to return. Must be greater than zero. The default is 5.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="QdrantSearchResponse"/>
    /// with the search results.</returns>
    Task<QdrantSearchResponse> SearchChunksInQdrantAsync(string model, string prompt, int topK = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously saves a text chunk associated with a specific model and chunk identifier to the specified file
    /// path, applying the provided tags.
    /// </summary>
    /// <param name="model">The name or identifier of the model to associate with the chunk. Cannot be null or empty.</param>
    /// <param name="chunkId">A unique identifier for the chunk being saved. Cannot be null or empty.</param>
    /// <param name="chunkText">The text content of the chunk to save. Cannot be null.</param>
    /// <param name="filePath">The full file system path where the chunk will be saved. Cannot be null or empty.</param>
    /// <param name="tags">A list of tags to associate with the chunk. Can be empty but cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task SaveChunkAsync(string model, string chunkId, string chunkText, string filePath, List<string> tags, CancellationToken cancellationToken = default);
}