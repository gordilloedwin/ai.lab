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
    /// Generates a prompt using the specified model and input text asynchronously.
    /// </summary>
    /// <param name="model">The identifier of the model to use for prompt generation. Cannot be null or empty.</param>
    /// <param name="prompt">The input text to be processed by the model. Cannot be null.</param>
    /// <param name="context">An optional array of context token IDs to provide additional information to the model. May be null if no context
    /// is required.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous prompt generation operation.</returns>
    Task GeneratePrompt(string model, string prompt, int[]? context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a prompt to the specified Ollama model and returns the generated response asynchronously.
    /// </summary>
    /// <param name="model">The name of the Ollama model to use for generating the response. Cannot be null or empty.</param>
    /// <param name="prompt">The input prompt to send to the model. Cannot be null or empty.</param>
    /// <param name="context">An optional array of context tokens to provide conversational history or additional context for the model. May be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the generated response as a string.</returns>
    Task<string> CallOllamaAsync(string model, string prompt, int[]? context, CancellationToken cancellationToken = default);
}
