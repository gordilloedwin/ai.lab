using ai.lab.service.Enum;

namespace ai.lab.service.Services.Common;

public interface IAIService
{
    /// <summary>
    /// Generates a prompt using the specified Ollama model asynchronously.
    /// </summary>
    /// <param name="model">The OllamaModel instance to use for generating the prompt. Cannot be null.</param>
    /// <param name="prompt">The prompt text to be processed by the model. Cannot be null or empty.</param>
    /// <returns>A task that represents the asynchronous operation of generating the prompt.</returns>
    Task GeneratePrompt(OllamaModel model, string prompt);

    /// <summary>
    /// Calls the Ollama service with the specified model and input prompt.
    /// </summary>
    /// <param name="model">The model to use (OllamaModel.DeepSeek or OllamaModel.Phi2).</param>
    /// <param name="prompt">The input string to send to the Ollama service.</param>
    /// <returns>The response from the Ollama service as a string.</returns>
    Task<string> CallOllamaAsync(OllamaModel model, string prompt);
}
