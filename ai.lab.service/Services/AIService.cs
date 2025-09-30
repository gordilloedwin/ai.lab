using ai.lab.service.Enum;
using ai.lab.service.Services.Common;

namespace ai.lab.service.Services;

public class AIService : IAIService
{
    public async Task<string> CallOllamaAsync(OllamaModel model, string prompt)
    {
        using var httpClient = new HttpClient();
        var ollamaModel = model switch
        {
            OllamaModel.DeepSeek => "deepseek",
            OllamaModel.Phi2 => "phi-2",
            _ => "phi-2"
        };

        var requestBody = new
        {
            model = ollamaModel,
            prompt = prompt
        };

        var content = new StringContent
        (
            System.Text.Json.JsonSerializer.Serialize(requestBody),
            System.Text.Encoding.UTF8, "application/json"
        );
        
        var response = await httpClient.PostAsync("http://localhost:11434/api/generate", content);
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();

        // Optionally, parse the response to extract the message
        using var doc = System.Text.Json.JsonDocument.Parse(responseString);
        if (doc.RootElement.TryGetProperty("response", out var message))
        {
            return message.GetString() ?? string.Empty;
        }

        return responseString;
    }
}
