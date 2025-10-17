namespace ai.lab.service.Model.Outbound;

/// <summary>
/// Response model for a chat message with sender information.
/// </summary>
public class ChatMessageResponse
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
    /// Email of the sender (NULL for AI messages).
    /// </summary>
    public string? SenderEmail { get; set; }

    /// <summary>
    /// Display name of the sender.
    /// </summary>
    public string? SenderName { get; set; }

    /// <summary>
    /// Avatar URI of the sender.
    /// </summary>
    public string? SenderAvatarUri { get; set; }

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

    /// <summary>
    /// Indicates whether this message is from the requesting user.
    /// </summary>
    public bool IsOwnMessage { get; set; }
}
