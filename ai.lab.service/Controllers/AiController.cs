using ai.lab.service.Services.Common;
using Microsoft.AspNetCore.Mvc;

namespace ai.lab.service.Controllers;

[ApiController]
[Route("[controller]")]
[Produces("application/json")]
public class AiController(IAIService aIService, ILogger<AiController> logger) : ControllerBase
{
    /// <summary>
    /// Retrieves the list of available AI models from the underlying service.
    /// </summary>
    /// <remarks>The response will have a status code of 200 (OK) and include the list of models on success.
    /// If the underlying AI service cannot be reached, a 502 (Bad Gateway) status code is returned. If the request
    /// times out, a 408 (Request Timeout) status code is returned. For other errors, a 500 (Internal Server Error)
    /// status code is returned. Error responses include details in the response body to assist with
    /// troubleshooting.</remarks>
    /// <returns>An <see cref="IActionResult"/> containing a collection of available AI models if the request is successful.
    /// Returns an error response with the appropriate HTTP status code if the service is unavailable, times out, or an
    /// unexpected error occurs.</returns>
    [HttpGet("models")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status408RequestTimeout)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAvailableModels(CancellationToken cancellationToken)
    {
        try
        {
            var models = await aIService.GetAvailableAiModels(cancellationToken);
            return Ok(models);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error occurred while retrieving available models");
            return StatusCode(StatusCodes.Status502BadGateway, new AiErrorResponse
            {
                Error = "Unable to connect to Ollama service",
                Details = ex.Message,
                Timestamp = DateTimeOffset.Now
            });
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "Timeout occurred while retrieving available models");
            return StatusCode(StatusCodes.Status408RequestTimeout, new AiErrorResponse
            {
                Error = "Request timeout while calling Ollama service",
                Details = ex.Message,
                Timestamp = DateTimeOffset.Now
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while retrieving available models");
            return StatusCode(StatusCodes.Status500InternalServerError, new AiErrorResponse
            {
                Error = "An unexpected error occurred",
                Details = ex.Message,
                Timestamp = DateTimeOffset.Now
            });
        }
    }

    /// <summary>
    /// Generates AI response using Ollama service
    /// </summary>
    /// <param name="request">The AI generation request containing model and prompt</param>
    /// <returns>AI generated response</returns>
    [HttpPost("generate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status408RequestTimeout)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GenerateResponse
    (
        [FromBody] AiGenerateRequest request,
        [FromQuery] bool showContext = false,
        [FromQuery] string model = "",
        CancellationToken cancellationToken = default
    )    
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

            model = string.IsNullOrWhiteSpace(model) ? "deepseek-coder:6.7b" : model;
            var forwardedHeader = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            var ipAddress = forwardedHeader ?? HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            var response = await aIService.CallOllamaAsync(model, request.Prompt, ipAddress, cancellationToken);

            if (!response.Success)
            {
                logger.LogWarning("Received empty response from Ollama service");
                return StatusCode(StatusCodes.Status502BadGateway, new AiErrorResponse
                {
                    Error = "Empty response from Ollama service",
                    Details = "The Ollama service returned an empty response.",
                    Timestamp = DateTimeOffset.Now
                });
            }
            
            return Ok(new AiGenerateResponse
            {
                Response = response.Response,
                Model = response.Model,
                Timestamp = response.Timestamp,
                Success = response.Success,
                Context = showContext ? response.Context : null
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