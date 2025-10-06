using ai.lab.service.Model.Inbound;
using Microsoft.AspNetCore.Mvc;

namespace ai.lab.service.Controllers;

[ApiController]
[Route("[controller]")]
[Produces("application/json")]
public class ServiceController(ILogger<ServiceController> _logger) : ControllerBase
{
    /// <summary>
    /// Gets the current status of the AI Lab service
    /// </summary>
    /// <returns>Service status information</returns>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            Status = "Running",
            Timestamp = DateTimeOffset.Now,
            ServiceType = "Hybrid Worker + Web API",
            Message = "Service is operational and accepting both background tasks and HTTP requests"
        });
    }

    /// <summary>
    /// Gets the health status of the AI Lab service
    /// </summary>
    /// <returns>Health status information</returns>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            Status = "Healthy",
            Uptime = DateTimeOffset.Now.ToString("O"),
            Environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"
        });
    }

    /// <summary>
    /// Logs a message through the service logger
    /// </summary>
    /// <param name="request">The log request containing the message</param>
    /// <returns>Confirmation of logged message</returns>
    [HttpPost("log")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult LogMessage([FromBody] LogRequest request)
    {
        if (string.IsNullOrEmpty(request?.Message))
        {
            return BadRequest("Message is required");
        }

        _logger.LogInformation("API Log: {Message}", request.Message);
        return Ok(new { Logged = true, Message = request.Message, Timestamp = DateTimeOffset.Now });
    }
}