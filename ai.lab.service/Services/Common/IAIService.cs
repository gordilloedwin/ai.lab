using ai.lab.service.Controllers;

namespace ai.lab.service.Services.Common;

public interface IAIService
{
    /// <summary>
    /// Asynchronously retrieves a list of available AI model names.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of strings, each representing
    /// the name of an available AI model. The list is empty if no models are available.</returns>
    Task<List<string>> GetAvailableAiModels(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a prompt to the specified Ollama model and asynchronously retrieves the generated AI response.
    /// </summary>
    /// <param name="model">The name of the Ollama model to use for generating the response. Cannot be null or empty.</param>
    /// <param name="prompt">The prompt text to send to the model for generation. Cannot be null or empty.</param>
    /// <param name="chatId">The identifier for the chat session. Used to maintain conversation context. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an <see cref="AiGenerateResponse"/>
    /// with the generated response from the model.</returns>
    Task<AiGenerateResponse> CallOllamaAsync(string model, string prompt, string chatId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams the generated response for a chat prompt as an asynchronous sequence of text segments.
    /// </summary>
    /// <remarks>The returned sequence yields response segments as they become available, allowing the caller
    /// to process the output incrementally. The method does not buffer the entire response before yielding results. If
    /// the operation is canceled via the provided token, the sequence will end early.</remarks>
    /// <param name="chatId">The unique identifier of the chat session for which the response is generated. Cannot be null or empty.</param>
    /// <param name="model">The name of the model to use for generating the response. Cannot be null or empty.</param>
    /// <param name="prompt">The prompt text to send to the model for generating a response. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the streaming operation.</param>
    /// <returns>An asynchronous sequence of strings representing segments of the generated response. The sequence completes when
    /// the full response has been streamed.</returns>
    IAsyncEnumerable<string> StreamResponse(string chatId, string model, string prompt, CancellationToken cancellationToken = default);
}
