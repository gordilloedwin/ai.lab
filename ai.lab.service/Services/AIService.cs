using ai.lab.service.Enum;
using ai.lab.service.Services.Common;
using Microsoft.AspNetCore.SignalR;

namespace ai.lab.service.Services;

public class AIService(ILogger<AIService> logger) : Hub, IAIService
{
    public async Task GeneratePrompt(OllamaModel model, string prompt)
    {
        try
        {
            logger.LogInformation("Starting prompt generation via SignalR. Model: {Model}, prompt length: {PromptLength}", 
                model, prompt?.Length ?? 0);

            if (string.IsNullOrWhiteSpace(prompt))
            {
                logger.LogWarning("Prompt generation called with null or empty prompt");
                await Clients.Caller.SendAsync("ReceiveError", new 
                { 
                    Error = "Invalid Input", 
                    Message = "Prompt cannot be null or empty",
                    Timestamp = DateTimeOffset.Now 
                });
                return;
            }

            var result = await CallOllamaAsync(model, prompt);
            
            logger.LogDebug("Sending response to SignalR caller, response length: {ResponseLength}", 
                result?.Length ?? 0);
            
            await Clients.Caller.SendAsync("ReceiveResponse", result);
            
            logger.LogInformation("Successfully sent response to SignalR caller");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Service error occurred during prompt generation. Model: {Model}, Prompt length: {PromptLength}", 
                model, prompt?.Length ?? 0);
            
            await Clients.Caller.SendAsync("ReceiveError", new 
            { 
                Error = "Service Error", 
                Message = ex.Message,
                Timestamp = DateTimeOffset.Now 
            });
        }
        catch (TimeoutException ex)
        {
            logger.LogError(ex, "Timeout occurred during prompt generation. Model: {Model}, Prompt length: {PromptLength}", 
                model, prompt?.Length ?? 0);
            
            await Clients.Caller.SendAsync("ReceiveError", new 
            { 
                Error = "Request Timeout", 
                Message = "The AI service took too long to respond. Please try again.",
                Timestamp = DateTimeOffset.Now 
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error occurred during prompt generation. Model: {Model}, Prompt length: {PromptLength}", 
                model, prompt?.Length ?? 0);
            
            await Clients.Caller.SendAsync("ReceiveError", new 
            { 
                Error = "Unexpected Error", 
                Message = "An unexpected error occurred. Please try again later.",
                Timestamp = DateTimeOffset.Now 
            });
        }
    }

    public async Task<string> CallOllamaAsync(OllamaModel model, string prompt)
    {
        try
        {
            logger.LogInformation("Starting Ollama API call with model: {Model}, prompt length: {PromptLength}", 
                model, prompt?.Length ?? 0);

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5); // Set reasonable timeout
            
            var requestBody = new { model, prompt };
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(requestBody),
                System.Text.Encoding.UTF8, "application/json");

            logger.LogDebug("Sending request to Ollama API at http://localhost:11434/api/generate");

            var response = await httpClient.PostAsync("http://localhost:11434/api/generate", content);
            response.EnsureSuccessStatusCode();
            
            var responseString = await response.Content.ReadAsStringAsync();
            logger.LogDebug("Received response from Ollama API, response length: {ResponseLength}", 
                responseString?.Length ?? 0);

            if (string.IsNullOrEmpty(responseString))
            {
                logger.LogWarning("Received empty response from Ollama API");
                return "No response from Ollama.";
            }

            using var doc = System.Text.Json.JsonDocument.Parse(responseString);
            if (doc.RootElement.TryGetProperty("response", out var message))
            {
                var result = message.GetString() ?? string.Empty;
                logger.LogInformation("Successfully processed Ollama response, result length: {ResultLength}", 
                    result.Length);
                return result;
            }

            logger.LogWarning("No 'response' property found in Ollama API response");
            return "No response from Ollama.";
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error occurred while calling Ollama API. Model: {Model}, Prompt length: {PromptLength}", 
                model, prompt?.Length ?? 0);
            throw new InvalidOperationException($"Failed to connect to Ollama service: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || ex.CancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Timeout occurred while calling Ollama API. Model: {Model}, Prompt length: {PromptLength}", 
                model, prompt?.Length ?? 0);
            throw new TimeoutException("Ollama API request timed out", ex);
        }
        catch (System.Text.Json.JsonException ex)
        {
            logger.LogError(ex, "JSON parsing error while processing Ollama API response. Model: {Model}, Prompt length: {PromptLength}", 
                model, prompt?.Length ?? 0);
            throw new InvalidOperationException($"Invalid JSON response from Ollama service: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error occurred while calling Ollama API. Model: {Model}, Prompt length: {PromptLength}", 
                model, prompt?.Length ?? 0);
            throw new InvalidOperationException($"Unexpected error calling Ollama service: {ex.Message}", ex);
        }
    }
}