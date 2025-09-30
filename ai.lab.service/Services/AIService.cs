using ai.lab.service.Enum;
using ai.lab.service.Services.Common;

namespace ai.lab.service.Services;

public class AIService : IAIService
{
    public async Task<string> CallOllamaAsync(OllamaModel model, string prompt)
    {
        // TODO: Implement call to Ollama service for DeepSeek or Phi2
        await Task.Yield();
        return "Ollama response stub";
    }
}
