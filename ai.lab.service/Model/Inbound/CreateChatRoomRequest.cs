using System.ComponentModel.DataAnnotations;

namespace ai.lab.service.Model.Inbound;

/// <summary>
/// Request model for creating a new chat room.
/// </summary>
public class CreateChatRoomRequest
{
    /// <summary>
    /// Display name of the chat room.
    /// </summary>
    [Required(ErrorMessage = "Title is required")]
    [StringLength(255, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 255 characters")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional: AI model to use for this room.
    /// </summary>
    [StringLength(100, ErrorMessage = "AI model name cannot exceed 100 characters")]
    public string? AiModel { get; set; }

    /// <summary>
    /// Optional: Maximum number of participants (defaults to 30, max 30).
    /// </summary>
    [Range(2, 30, ErrorMessage = "Maximum participants must be between 2 and 30")]
    public int? MaxParticipants { get; set; }
}
