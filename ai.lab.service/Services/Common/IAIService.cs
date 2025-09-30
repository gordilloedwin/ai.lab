using ai.lab.service.Enum;

namespace ai.lab.service.Services.Common;

public interface IAIService
{
    /// <summary>
    /// Calls the Ollama service with the specified model and input prompt.
    /// </summary>
    /// <param name="model">The model to use (OllamaModel.DeepSeek or OllamaModel.Phi2).</param>
    /// <param name="prompt">The input string to send to the Ollama service.</param>
    /// <returns>The response from the Ollama service as a string.</returns>
    Task<string> CallOllamaAsync(OllamaModel model, string prompt);
}
