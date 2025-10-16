namespace ai.lab.service.Model.Database;

/// <summary>
/// Represents a single message in a chat room, sent by either a user or the AI.
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// Unique identifier for the message.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The chat room this message belongs to.
    /// </summary>
    public long ChatRoomId { get; set; }

    /// <summary>
    /// Email of the user who sent this message (NULL for AI messages).
    /// </summary>
    public string? SenderEmail { get; set; }

    /// <summary>
    /// Type of sender: "user" or "ai".
    /// </summary>
    public string SenderType { get; set; } = string.Empty;

    /// <summary>
    /// The text content of the message.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the message was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
