namespace ai.lab.service.Model.Outbound;

/// <summary>
/// Response model for a chat participant with user information.
/// </summary>
public class ChatParticipantResponse
{
    /// <summary>
    /// Email of the participant.
    /// </summary>
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the participant.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Avatar URI of the participant.
    /// </summary>
    public string? AvatarUri { get; set; }

    /// <summary>
    /// Timestamp when the participant joined the room.
    /// </summary>
    public DateTime JoinedAt { get; set; }

    /// <summary>
    /// Timestamp when the participant left the room (NULL if still in room).
    /// </summary>
    public DateTime? LeftAt { get; set; }

    /// <summary>
    /// Indicates whether the participant is currently connected.
    /// </summary>
    public bool IsCurrentlyConnected { get; set; }

    /// <summary>
    /// Timestamp of the participant's last activity.
    /// </summary>
    public DateTime? LastSeenAt { get; set; }

    /// <summary>
    /// Duration the participant has been in the room (in seconds).
    /// </summary>
    public long TimeInRoomSeconds { get; set; }

    /// <summary>
    /// Indicates whether this participant is the requesting user.
    /// </summary>
    public bool IsCurrentUser { get; set; }
}
