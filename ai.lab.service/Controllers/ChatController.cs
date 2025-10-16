using ai.lab.service.Model.Inbound;
using ai.lab.service.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ai.lab.service.Controllers;

/// <summary>
/// REST API endpoints for chat room management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    private string GetUserEmail()
    {
        return User.FindFirstValue(ClaimTypes.Email) 
               ?? throw new UnauthorizedAccessException("User email not found in claims");
    }

    #region Room Management

    /// <summary>
    /// Create a new chat room.
    /// </summary>
    [HttpPost("rooms")]
    public async Task<IActionResult> CreateChatRoom([FromBody] CreateChatRoomRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userEmail = GetUserEmail();
            var room = await _chatService.CreateChatRoomAsync(
                userEmail, 
                request.Title, 
                request.AiModel, 
                request.MaxParticipants, 
                cancellationToken);
            
            return Ok(room);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create chat room");
            return StatusCode(500, new { error = "Failed to create chat room" });
        }
    }

    /// <summary>
    /// Get all chat rooms the user is currently in.
    /// </summary>
    [HttpGet("rooms/mine")]
    public async Task<IActionResult> GetMyChatRooms(CancellationToken cancellationToken)
    {
        try
        {
            var userEmail = GetUserEmail();
            var rooms = await _chatService.GetUserChatRoomsAsync(userEmail, cancellationToken);
            return Ok(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user chat rooms");
            return StatusCode(500, new { error = "Failed to get chat rooms" });
        }
    }

    /// <summary>
    /// Get all active chat rooms available to join.
    /// </summary>
    [HttpGet("rooms")]
    public async Task<IActionResult> GetAllChatRooms(CancellationToken cancellationToken)
    {
        try
        {
            var userEmail = GetUserEmail();
            var rooms = await _chatService.GetAllActiveChatRoomsAsync(userEmail, cancellationToken);
            return Ok(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all chat rooms");
            return StatusCode(500, new { error = "Failed to get chat rooms" });
        }
    }

    /// <summary>
    /// Get details of a specific chat room.
    /// </summary>
    [HttpGet("rooms/{roomId}")]
    public async Task<IActionResult> GetChatRoom(long roomId, CancellationToken cancellationToken)
    {
        try
        {
            var userEmail = GetUserEmail();
            var room = await _chatService.GetChatRoomByIdAsync(roomId, userEmail, cancellationToken);
            
            if (room == null)
            {
                return NotFound(new { error = "Chat room not found" });
            }
            
            return Ok(room);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get chat room {RoomId}", roomId);
            return StatusCode(500, new { error = "Failed to get chat room" });
        }
    }

    /// <summary>
    /// Delete a chat room (soft delete, creator only).
    /// </summary>
    [HttpDelete("rooms/{roomId}")]
    public async Task<IActionResult> DeleteChatRoom(long roomId, CancellationToken cancellationToken)
    {
        try
        {
            var userEmail = GetUserEmail();
            var success = await _chatService.DeleteChatRoomAsync(roomId, userEmail, cancellationToken);
            
            if (!success)
            {
                return BadRequest(new { error = "Unable to delete chat room. You must be the creator." });
            }
            
            return Ok(new { message = "Chat room deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete chat room {RoomId}", roomId);
            return StatusCode(500, new { error = "Failed to delete chat room" });
        }
    }

    #endregion

    #region Participant Management

    /// <summary>
    /// Get participants in a chat room.
    /// </summary>
    [HttpGet("rooms/{roomId}/participants")]
    public async Task<IActionResult> GetParticipants(long roomId, [FromQuery] bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var userEmail = GetUserEmail();
            var participants = await _chatService.GetChatParticipantsAsync(roomId, userEmail, activeOnly, cancellationToken);
            return Ok(participants);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get participants for room {RoomId}", roomId);
            return StatusCode(500, new { error = "Failed to get participants" });
        }
    }

    /// <summary>
    /// Join a chat room (alternative to SignalR hub method for REST clients).
    /// </summary>
    [HttpPost("rooms/{roomId}/join")]
    public async Task<IActionResult> JoinRoom(long roomId, CancellationToken cancellationToken)
    {
        try
        {
            var userEmail = GetUserEmail();
            // Use a temporary connection ID for REST joins
            var connectionId = $"rest-{Guid.NewGuid()}";
            var success = await _chatService.JoinChatRoomAsync(roomId, userEmail, connectionId, cancellationToken);
            
            if (!success)
            {
                return BadRequest(new { error = "Unable to join room. Room may be full or inactive." });
            }
            
            return Ok(new { message = "Successfully joined chat room" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join room {RoomId}", roomId);
            return StatusCode(500, new { error = "Failed to join room" });
        }
    }

    /// <summary>
    /// Leave a chat room (alternative to SignalR hub method for REST clients).
    /// </summary>
    [HttpPost("rooms/{roomId}/leave")]
    public async Task<IActionResult> LeaveRoom(long roomId, CancellationToken cancellationToken)
    {
        try
        {
            var userEmail = GetUserEmail();
            var success = await _chatService.LeaveChatRoomAsync(roomId, userEmail, cancellationToken);
            
            if (!success)
            {
                return BadRequest(new { error = "Unable to leave room. You may not be in this room." });
            }
            
            return Ok(new { message = "Successfully left chat room" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to leave room {RoomId}", roomId);
            return StatusCode(500, new { error = "Failed to leave room" });
        }
    }

    #endregion

    #region Message Management

    /// <summary>
    /// Get messages from a chat room with pagination.
    /// </summary>
    [HttpGet("rooms/{roomId}/messages")]
    public async Task<IActionResult> GetMessages(
        long roomId, 
        [FromQuery] int limit = 100, 
        [FromQuery] long? beforeMessageId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userEmail = GetUserEmail();
            var messages = await _chatService.GetChatMessagesAsync(roomId, userEmail, limit, beforeMessageId, cancellationToken);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get messages for room {RoomId}", roomId);
            return StatusCode(500, new { error = "Failed to get messages" });
        }
    }

    /// <summary>
    /// Send a message to a chat room (alternative to SignalR hub method for REST clients).
    /// </summary>
    [HttpPost("rooms/{roomId}/messages")]
    public async Task<IActionResult> SendMessage(
        long roomId, 
        [FromBody] SendChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userEmail = GetUserEmail();
            var message = await _chatService.AddUserMessageAsync(roomId, userEmail, request.Content, cancellationToken);
            return Ok(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to room {RoomId}", roomId);
            return StatusCode(500, new { error = "Failed to send message" });
        }
    }

    #endregion

    #region Read Receipts

    /// <summary>
    /// Update read receipt for a chat room.
    /// </summary>
    [HttpPost("rooms/{roomId}/read")]
    public async Task<IActionResult> UpdateReadReceipt(
        long roomId, 
        [FromBody] UpdateReadReceiptRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userEmail = GetUserEmail();
            await _chatService.UpdateReadReceiptAsync(roomId, userEmail, request.LastReadMessageId, cancellationToken);
            return Ok(new { message = "Read receipt updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update read receipt for room {RoomId}", roomId);
            return StatusCode(500, new { error = "Failed to update read receipt" });
        }
    }

    /// <summary>
    /// Get unread message count for a chat room.
    /// </summary>
    [HttpGet("rooms/{roomId}/unread")]
    public async Task<IActionResult> GetUnreadCount(long roomId, CancellationToken cancellationToken)
    {
        try
        {
            var userEmail = GetUserEmail();
            var count = await _chatService.GetUnreadMessageCountAsync(roomId, userEmail, cancellationToken);
            return Ok(new { unreadCount = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get unread count for room {RoomId}", roomId);
            return StatusCode(500, new { error = "Failed to get unread count" });
        }
    }

    #endregion
}

/// <summary>
/// Request model for updating read receipts.
/// </summary>
public class UpdateReadReceiptRequest
{
    public long LastReadMessageId { get; set; }
}
