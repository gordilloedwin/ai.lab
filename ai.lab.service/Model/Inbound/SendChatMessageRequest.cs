using System.ComponentModel.DataAnnotations;

namespace ai.lab.service.Model.Inbound;

/// <summary>
/// Request model for sending a message in a chat room.
/// </summary>
public class SendChatMessageRequest
{
    /// <summary>
    /// The text content of the message.
    /// </summary>
    [Required(ErrorMessage = "Message content is required")]
    [StringLength(10000, MinimumLength = 1, ErrorMessage = "Message must be between 1 and 10000 characters")]
    public string Content { get; set; } = string.Empty;
}
