namespace ai.lab.service.Model.Database;

/// <summary>
/// Tracks the last message a user has read in a chat room for unread message counting.
/// </summary>
public class ChatReadReceipt
{
    /// <summary>
    /// Unique identifier for this read receipt.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The chat room this read receipt belongs to.
    /// </summary>
    public long ChatRoomId { get; set; }

    /// <summary>
    /// Email of the user who read the messages.
    /// </summary>
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// ID of the last message the user has read in this room.
    /// </summary>
    public long LastReadMessageId { get; set; }

    /// <summary>
    /// Timestamp when the read receipt was last updated.
    /// </summary>
    public DateTime ReadAt { get; set; }
}
