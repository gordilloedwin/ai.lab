using ai.lab.service.Managers.Common;
using ai.lab.service.Models.Ollama;
using ai.lab.service.Options;
using ai.lab.service.Services.Common;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ai.lab.service.Managers;

public class OllamaClientManager : AILabBaseClient, IOllamaClient
{
    public static new string HttpClientName => "OllamaClient";

    private readonly ILogger<OllamaClientManager> _logger = null!;

    private readonly IOptionsMonitor<AILabOptions> _options = null!;

    public OllamaClientManager(IHttpClientFactory httpClientFactory, IOptionsMonitor<AILabOptions> options, ILogger<OllamaClientManager> logger)
        : base(logger, httpClientFactory)
    {
        _logger = logger;
        _options = options;
    }

    public async Task<List<string>> GetAvailableAiModels(CancellationToken cancellationToken = default)
    {
        try
        {
            HttpClient.Timeout = TimeSpan.FromMinutes(5);
            var response = await HttpClient.GetAsync($"{_options.CurrentValue.OllamaUrl}/api/tags", cancellationToken);
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            var dto = OllamaTagsResponse.FromJson(responseString);
            var modelNames = dto?.Models.Select(m => m.Name).ToList() ?? new List<string>();

            return modelNames;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex.Message);
            throw new InvalidOperationException($"Failed to connect to Ollama service: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || ex.CancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex.Message);
            throw new TimeoutException("Ollama API request timed out", ex);
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex.Message);
            throw new InvalidOperationException($"Invalid JSON response from Ollama service: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new InvalidOperationException($"Unexpected error calling Ollama service: {ex.Message}", ex);
        }
    }

    public async Task<string> CallOllamaApiAsync(string model, string prompt, int[]? context, CancellationToken cancellationToken = default)
    {
        try
        {
            var stream = false;
            HttpClient.Timeout = TimeSpan.FromMinutes(10);
            var requestBody = new { model, prompt, stream, context };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync($"{_options.CurrentValue.OllamaUrl}/api/generate", content, cancellationToken);
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(responseString))
            {
                _logger.LogWarning("Ollama API returned empty response. Model: {Model}, Prompt length: {PromptLength}", model, prompt?.Length ?? 0);
                return string.Empty;
            }
            
            return responseString;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while calling Ollama API. Model: {Model}, Prompt length: {PromptLength}", model, prompt?.Length ?? 0);
            throw new InvalidOperationException($"Failed to connect to Ollama service: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || ex.CancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Timeout occurred while calling Ollama API. Model: {Model}, Prompt length: {PromptLength}", model, prompt?.Length ?? 0);
            throw new TimeoutException("Ollama API request timed out", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while calling Ollama API. Model: {Model}, Prompt length: {PromptLength}", model, prompt?.Length ?? 0);
            throw new InvalidOperationException($"Unexpected error calling Ollama service: {ex.Message}", ex);
        }
    }
}
