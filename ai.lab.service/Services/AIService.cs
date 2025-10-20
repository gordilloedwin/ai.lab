using ai.lab.service.Managers.Common;
using ai.lab.service.Model.Outbound;
using ai.lab.service.Options;
using ai.lab.service.Services.Common;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ai.lab.service.Services;

public sealed class AIService
(
    ILogger<AIService> logger,
    IOllamaClient ollamaClient,
    IEmbeddingManager embeddingManager,
    IOptionsMonitor<AILabOptions> options,
    IContextSessionManager sessionManager
) : IAIService
{
    /// <inheritdoc/>
    public async Task<List<string>> GetAvailableAiModels(CancellationToken cancellationToken = default) => 
        await ollamaClient.GetAvailableAiModels(cancellationToken);

    /// <inheritdoc/>
    public async Task<AiGenerateResponse> GenerateResponseFromApiAsync(string model, string prompt, string email, CancellationToken cancellationToken = default)
    {
        try
        {
            var context = sessionManager != null ? (await sessionManager.GetContextAsync(email, model, cancellationToken))?.ToArray() ?? [] : [];
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
            if (sessionManager != null)
            {
                await sessionManager.StoreContextAsync(email, model, aiServiceResponse?.Context?.ToList() ?? [], cancellationToken);
            }

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
    public async Task<AiGenerateResponse> GenerateResponseFromRagAsync(string model, string prompt, string email, CancellationToken cancellationToken = default)
    {
        try
        {
            var embeddings = await embeddingManager.SearchChunksAsync(model, prompt, options.CurrentValue.MaxRagChunksPerPrompt, cancellationToken);
            var qdrantContextWindow = new QdrantContextBuilder(embeddings).BuildContextWindow();
            string finalPrompt = $"Use the following context to answer the question.\n\nContext:\n{qdrantContextWindow}\n\nQuestion:\n{prompt}\n\nAnswer:";
            return await GenerateResponseFromApiAsync(model, finalPrompt, email, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error occurred while calling Ollama or Qdrant service for RAG. Model: {Model}, Prompt length: {PromptLength}", 
                model, prompt?.Length ?? 0);
            throw new InvalidOperationException($"Failed to connect to Ollama or Qdrant service: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || ex.CancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Timeout occurred while calling Ollama or Qdrant service for RAG. Model: {Model}, Prompt length: {PromptLength}", 
                model, prompt?.Length ?? 0);
            throw new TimeoutException("Ollama API request timed out", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error occurred while calling Ollama or Qdrant service for RAG. Model: {Model}, Prompt length: {PromptLength}", 
                model, prompt?.Length ?? 0);
            throw new InvalidOperationException($"Unexpected error calling Ollama service: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> StreamResponseAsync(string email, string model, string prompt, [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
        var context = sessionManager != null ? (await sessionManager.GetContextAsync(email, model, cancellationToken))?.ToArray() ?? [] : [];
        await process.StandardInput.WriteLineAsync(prompt);

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
                    logger.LogError(ex, "Error killing Ollama process for email: {ChatId}", email);
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
                if (sessionManager != null)
                {
                    await sessionManager.StoreContextAsync(email, model, chunkContext, cancellationToken);
                }

                //await Task.Delay(30, cancellationToken);
                yield return chunk;
            }
            else
            {
                yield return line;
            }
        }

        yield return "\n\n[[DONE]]";
    }
}