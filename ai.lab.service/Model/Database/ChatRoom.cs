namespace ai.lab.service.Model.Database;

/// <summary>
/// Represents a chat room where multiple users can communicate with each other and with AI.
/// </summary>
public class ChatRoom
{
    /// <summary>
    /// Unique identifier for the chat room.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Display name of the chat room.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Email of the user who created this chat room.
    /// </summary>
    public string CreatedByEmail { get; set; } = string.Empty;

    /// <summary>
    /// The AI model used for generating responses in this chat room.
    /// </summary>
    public string AiModel { get; set; } = "deepseek-coder:6.7b";

    /// <summary>
    /// Maximum number of participants allowed in this room (default: 30).
    /// </summary>
    public int MaxParticipants { get; set; } = 30;

    /// <summary>
    /// Timestamp when the chat room was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the chat room was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Indicates whether the chat room is active and accepting new participants.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
