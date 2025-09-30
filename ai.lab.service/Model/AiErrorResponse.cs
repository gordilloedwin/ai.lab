namespace ai.lab.service.Controllers;

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