namespace ai.lab.service.Model.Outbound;

/// <summary>
/// Response model containing chat room details with current statistics.
/// </summary>
public class ChatRoomResponse
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
    /// Email of the user who created this room.
    /// </summary>
    public string CreatedByEmail { get; set; } = string.Empty;

    /// <summary>
    /// Name of the user who created this room.
    /// </summary>
    public string? CreatedByName { get; set; }

    /// <summary>
    /// AI model used in this room.
    /// </summary>
    public string AiModel { get; set; } = string.Empty;

    /// <summary>
    /// Maximum participants allowed.
    /// </summary>
    public int MaxParticipants { get; set; }

    /// <summary>
    /// Number of currently connected participants.
    /// </summary>
    public int CurrentParticipantCount { get; set; }

    /// <summary>
    /// Total number of messages in the room.
    /// </summary>
    public int TotalMessageCount { get; set; }

    /// <summary>
    /// Number of unread messages for the requesting user.
    /// </summary>
    public int UnreadMessageCount { get; set; }

    /// <summary>
    /// Timestamp when the room was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp of the last message in this room.
    /// </summary>
    public DateTime? LastMessageAt { get; set; }

    /// <summary>
    /// Indicates whether the room is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Indicates whether the room is at maximum capacity.
    /// </summary>
    public bool IsFull { get; set; }

    /// <summary>
    /// Indicates whether the requesting user is currently in this room.
    /// </summary>
    public bool IsUserInRoom { get; set; }
}
