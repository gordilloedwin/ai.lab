using System.ComponentModel.DataAnnotations;

namespace ai.lab.service.Controllers;

/// <summary>
/// Request model for logging messages
/// </summary>
public class LogRequest
{
    /// <summary>
    /// The message to be logged
    /// </summary>
    [Required(ErrorMessage = "Message is required")]
    [StringLength(1000, ErrorMessage = "Message cannot exceed 1000 characters")]
    public string? Message { get; set; }
}