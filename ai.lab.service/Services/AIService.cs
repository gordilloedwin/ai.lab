using ai.lab.service.Enum;
using ai.lab.service.Services.Common;
using Microsoft.AspNetCore.SignalR;

namespace ai.lab.service.Services;

public class AIService : Hub, IAIService
{
    public async Task GeneratePrompt(OllamaModel model, string prompt)
    {
        var result = await CallOllamaAsync(model, prompt);
        await Clients.Caller.SendAsync("ReceiveResponse", result);
    }

    public async Task<string> CallOllamaAsync(OllamaModel model, string prompt)
    {
        using var httpClient = new HttpClient();
        var requestBody = new { model, prompt };
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(requestBody),
            System.Text.Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync("http://localhost:11434/api/generate", content);
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();

        using var doc = System.Text.Json.JsonDocument.Parse(responseString);
        if (doc.RootElement.TryGetProperty("response", out var message))
        {
            return message.GetString() ?? string.Empty;
        }

        return "No response from Ollama.";
    }
}
