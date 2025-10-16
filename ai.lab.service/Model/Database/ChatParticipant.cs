namespace ai.lab.service.Model.Database;

/// <summary>
/// Represents a user's participation in a chat room, including presence and connection state.
/// </summary>
public class ChatParticipant
{
    /// <summary>
    /// Unique identifier for this participation record.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The chat room this participation belongs to.
    /// </summary>
    public long ChatRoomId { get; set; }

    /// <summary>
    /// Email of the participating user.
    /// </summary>
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the user joined this chat room.
    /// </summary>
    public DateTime JoinedAt { get; set; }

    /// <summary>
    /// Timestamp when the user explicitly left the chat room (NULL if still in room).
    /// </summary>
    public DateTime? LeftAt { get; set; }

    /// <summary>
    /// Indicates whether the user is currently connected via SignalR.
    /// </summary>
    public bool IsCurrentlyConnected { get; set; }

    /// <summary>
    /// SignalR connection ID for real-time presence tracking.
    /// </summary>
    public string? ConnectionId { get; set; }

    /// <summary>
    /// Timestamp of the user's last activity in this room.
    /// </summary>
    public DateTime? LastSeenAt { get; set; }
}
