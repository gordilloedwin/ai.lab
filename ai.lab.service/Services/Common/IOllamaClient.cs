using ai.lab.service.Model.Semantics;

namespace ai.lab.service.Services.Common;

public interface IOllamaClient
{
    /// <summary>
    /// Asynchronously retrieves a list of available AI model names.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of strings, each representing
    /// the name of an available AI model. The list is empty if no models are available.</returns>
    Task<List<string>> GetAvailableAiModels(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a prompt to the specified Ollama model and asynchronously returns the generated response as a string.
    /// </summary>
    /// <param name="model">The name of the Ollama model to use for generating the response. Cannot be null or empty.</param>
    /// <param name="prompt">The prompt text to send to the model for completion. Cannot be null or empty.</param>
    /// <param name="context">An optional array of context token IDs to provide conversational history or context for the model. May be null
    /// if no context is required.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the generated response from the
    /// model as a string.</returns>
    Task<string> CallOllamaApiAsync(string model, string prompt, int[]? context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an embedding response for the specified text chunk using the given model asynchronously.
    /// </summary>
    /// <param name="model">The name or identifier of the embedding model to use for generating the response. Cannot be null or empty.</param>
    /// <param name="chunkText">The text content to be embedded. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an <see cref="EmbeddingResponse"/>
    /// with the generated embedding data.</returns>
    Task<EmbeddingResponse> GenerateEmbeddingResponseAsync(string model, string chunkText, CancellationToken cancellationToken = default);
}
