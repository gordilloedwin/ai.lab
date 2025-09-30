using ai.lab.service.Services.Common;
using Microsoft.AspNetCore.Mvc;

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