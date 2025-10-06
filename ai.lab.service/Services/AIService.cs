using ai.lab.service.Model.Outbound;
using ai.lab.service.Services.Common;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ai.lab.service.Services;

public sealed class AIService
(
    ILogger<AIService> logger,
    IOllamaClient ollamaClient,
    IContextSessionManager sessionManager
) : Hub, IAIService
{
    /// <inheritdoc/>
    public async Task<List<string>> GetAvailableAiModels(CancellationToken cancellationToken = default) => 
        await ollamaClient.GetAvailableAiModels(cancellationToken);

    /// <inheritdoc/>
    public async Task<AiGenerateResponse> GenerateResponseFromApiAsync(string model, string prompt, string chatId, CancellationToken cancellationToken = default)
    {
        try
        {
            var context = sessionManager?.GetContext(chatId)?.ToArray() ?? [];
            var responseString = await ollamaClient.CallOllamaApiAsync(model, prompt, context, cancellationToken);

            if (string.IsNullOrWhiteSpace(responseString))
            {
                logger.LogWarning("Ollama API returned empty response. Model: {Model}, Prompt length: {PromptLength}", model, prompt?.Length ?? 0);
                return new AiGenerateResponse
                {
                    Model = model,
                    Timestamp = DateTimeOffset.Now,
                    Success = false,
                    Response = string.Empty,
                    Context = context
                };
            }

            var aiServiceResponse = new AiGenerateResponse()
            {
                Model = model,
                Timestamp = DateTimeOffset.Now,
                Success = true
            };

            string result = string.Empty;
            using var doc = JsonDocument.Parse(responseString);            
            aiServiceResponse.Context = doc.RootElement.GetProperty("context").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            aiServiceResponse.Response = doc.RootElement.TryGetProperty("response", out var message) ? (message.GetString() ?? string.Empty) : string.Empty;
            sessionManager?.StoreContext(chatId, aiServiceResponse?.Context?.ToList() ?? []); 
            return aiServiceResponse ?? new AiGenerateResponse();
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
        catch (JsonException ex)
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

        while (!process.StandardOutput.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await process.StandardOutput.ReadLineAsync();
            if (line is null) continue;


            if (cancellationToken.IsCancellationRequested)
            {
                try 
                {
                    process.Kill();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error killing Ollama process for chatId: {ChatId}", chatId);
                }
                
                yield break;
            }

            string? chunk = null;
            List<int>? chunkContext = null;
            bool parseSuccess = false;

            try
            {
                var json = JsonDocument.Parse(line);
                chunk = json.RootElement.GetProperty("response").GetString();
                chunkContext = json.RootElement.GetProperty("context").EnumerateArray().Select(x => x.GetInt32()).ToList();
                parseSuccess = true;
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Failed to parse JSON from Ollama stream: {Line}", line);
            }

            if (parseSuccess && chunk != null && chunkContext != null)
            {
                sessionManager?.StoreContext(chatId, chunkContext);
                await Task.Delay(30, cancellationToken);
                yield return chunk;
            }
        }

        yield return "[[DONE]]";
    }
}