using ai.lab.service.Services.Common;
using ai.lab.service.Enum;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace ai.lab.service.Controllers;

[ApiController]
[Route("[controller]")]
[Produces("application/json")]
public class AiController(IAIService aIService, ILogger<AiController> logger) : ControllerBase
{
    /// <summary>
    /// Generates AI response using Ollama service
    /// </summary>
    /// <param name="request">The AI generation request containing model and prompt</param>
    /// <returns>AI generated response</returns>
    [HttpPost("generate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GenerateResponse([FromBody] AiGenerateRequest request)
    {
        try
        {
            if (request == null)
            {
                logger.LogWarning("Generate request is null");
                return BadRequest("Request cannot be null");
            }

            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                logger.LogWarning("Prompt is null or empty");
                return BadRequest("Prompt is required");
            }

            logger.LogInformation("Generating AI response for model: {Model}, prompt length: {PromptLength}", 
                request.Model, request.Prompt.Length);

            var response = await aIService.CallOllamaAsync(request.Model, request.Prompt);

            logger.LogInformation("AI response generated successfully, response length: {ResponseLength}", 
                response?.Length ?? 0);

            return Ok(new AiGenerateResponse
            {
                Response = response,
                Model = request.Model.ToString(),
                Timestamp = DateTimeOffset.Now,
                Success = true
            });
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error occurred while calling Ollama service");
            return StatusCode(StatusCodes.Status502BadGateway, new AiErrorResponse
            {
                Error = "Unable to connect to Ollama service",
                Details = ex.Message,
                Timestamp = DateTimeOffset.Now
            });
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "Timeout occurred while calling Ollama service");
            return StatusCode(StatusCodes.Status408RequestTimeout, new AiErrorResponse
            {
                Error = "Request timeout while calling Ollama service",
                Details = ex.Message,
                Timestamp = DateTimeOffset.Now
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error occurred while generating AI response");
            return StatusCode(StatusCodes.Status500InternalServerError, new AiErrorResponse
            {
                Error = "An unexpected error occurred",
                Details = ex.Message,
                Timestamp = DateTimeOffset.Now
            });
        }
    }
}

/// <summary>
/// Request model for AI generation
/// </summary>
public class AiGenerateRequest
{
    /// <summary>
    /// The Ollama model to use for generation
    /// </summary>
    [Required(ErrorMessage = "Model is required")]
    public OllamaModel Model { get; set; }

    /// <summary>
    /// The prompt text to be processed
    /// </summary>
    [Required(ErrorMessage = "Prompt is required")]
    [StringLength(10000, ErrorMessage = "Prompt cannot exceed 10000 characters")]
    public string Prompt { get; set; } = string.Empty;
}

/// <summary>
/// Response model for successful AI generation
/// </summary>
public class AiGenerateResponse
{
    /// <summary>
    /// The generated AI response
    /// </summary>
    public string? Response { get; set; }

    /// <summary>
    /// The model used for generation
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of the response
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Indicates if the generation was successful
    /// </summary>
    public bool Success { get; set; }
}

/// <summary>
/// Response model for AI generation errors
/// </summary>
public class AiErrorResponse
{
    /// <summary>
    /// Error message
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Detailed error information
    /// </summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of the error
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }
}