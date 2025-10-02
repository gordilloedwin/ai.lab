namespace ai.lab.service.Controllers;

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

    /// <summary>
    /// Gets or sets the context information associated with the current operation.
    /// </summary>
    public int[]? Context { get; set; }
}