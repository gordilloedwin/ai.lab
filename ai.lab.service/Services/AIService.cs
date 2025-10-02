using ai.lab.service.Models.Ollama;
using ai.lab.service.Services.Common;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ai.lab.service.Services;

public class AIService(IOllamaSessionManager sessionManager, ILogger<AIService> logger) : Hub, IAIService
{
    /// <inheritdoc/>
    public async Task<List<string>> GetAvailableAiModels(CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5);
            var response = await httpClient.GetAsync("http://localhost:11434/api/tags", cancellationToken);

            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            var dto = OllamaTagsResponse.FromJson(responseString);
            var modelNames = dto?.Models.Select(m => m.Name).ToList() ?? new List<string>();

            return modelNames;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex.Message);
            throw new InvalidOperationException($"Failed to connect to Ollama service: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || ex.CancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex.Message);
            throw new TimeoutException("Ollama API request timed out", ex);
        }
        catch (System.Text.Json.JsonException ex)
        {
            logger.LogError(ex.Message);
            throw new InvalidOperationException($"Invalid JSON response from Ollama service: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
            throw new InvalidOperationException($"Unexpected error calling Ollama service: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<string> CallOllamaAsync(string model, string prompt, int[]? context, CancellationToken cancellationToken = default)
    {
        try
        {
            var stream = false;
            logger.LogInformation("Starting Ollama API call with model: {Model}, prompt length: {PromptLength}", 
                model, prompt?.Length ?? 0);

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(10);
            
            var requestBody = new { model, prompt, stream, context };
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(requestBody),
                System.Text.Encoding.UTF8, "application/json");

            logger.LogDebug("Sending request to Ollama API at http://localhost:11434/api/generate");

            var response = await httpClient.PostAsync("http://localhost:11434/api/generate", content, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var responseString = await response.Content.ReadAsStringAsync();
            logger.LogDebug("Received response from Ollama API, response length: {ResponseLength}", responseString?.Length ?? 0);

            if (string.IsNullOrEmpty(responseString))
            {
                logger.LogWarning("Received empty response from Ollama API");
                return string.Empty;
            }

            return responseString;
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

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> StreamResponse(string chatId, string model, string prompt, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ollama",
            Arguments = $"run {model}",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        var context = sessionManager?.GetContext(chatId)?.ToArray() ?? [];
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new
        {
            model,
            prompt,
            stream = true,
            context
        }));
        process.StandardInput.Close();

        while (!process.StandardOutput.EndOfStream)
        {
            var line = await process.StandardOutput.ReadLineAsync();
            if (line is null) continue;

            var json = JsonDocument.Parse(line);
            var chunk = json.RootElement.GetProperty("response").GetString();
            var chunkContext = json.RootElement.GetProperty("context").EnumerateArray().Select(x => x.GetInt32()).ToList();
            sessionManager?.StoreContext(chatId, chunkContext);

            yield return chunk!;
        }
    }


}