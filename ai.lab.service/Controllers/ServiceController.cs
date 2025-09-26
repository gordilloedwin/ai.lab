using Microsoft.AspNetCore.Mvc;

namespace ai.lab.service.Controllers;

[ApiController]
[Route("[controller]")]
public class ServiceController : ControllerBase
{
    private readonly ILogger<ServiceController> _logger;

    public ServiceController(ILogger<ServiceController> _logger)
    {
        this._logger = _logger;
    }

    [HttpGet("status")]
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

    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            Status = "Healthy",
            Uptime = DateTimeOffset.Now.ToString("O"),
            Environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"
        });
    }

    [HttpPost("log")]
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

public class LogRequest
{
    public string? Message { get; set; }
}