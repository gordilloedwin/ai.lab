using ai.lab.service.Model.Database;
using ai.lab.service.Model.Outbound;

namespace ai.lab.service.Services.Common;

/// <summary>
/// Service interface for managing multi-user chat rooms with AI participant.
/// </summary>
public interface IChatService
{
    #region Room Management

    /// <summary>
    /// Creates a new chat room.
    /// </summary>
    /// <param name="userEmail">Email of the user creating the room.</param>
    /// <param name="title">Title of the chat room.</param>
    /// <param name="aiModel">Optional AI model to use (defaults to deepseek-coder:6.7b).</param>
    /// <param name="maxParticipants">Optional max participants (defaults to 30).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created chat room with statistics.</returns>
    Task<ChatRoomResponse> CreateChatRoomAsync(string userEmail, string title, string? aiModel = null, int? maxParticipants = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all chat rooms the user has joined (not left).
    /// </summary>
    /// <param name="userEmail">Email of the user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of chat rooms with statistics.</returns>
    Task<List<ChatRoomResponse>> GetUserChatRoomsAsync(string userEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active chat rooms available to join.
    /// </summary>
    /// <param name="userEmail">Email of the requesting user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all active chat rooms.</returns>
    Task<List<ChatRoomResponse>> GetAllActiveChatRoomsAsync(string userEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed information about a specific chat room.
    /// </summary>
    /// <param name="chatRoomId">ID of the chat room.</param>
    /// <param name="userEmail">Email of the requesting user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Chat room details or null if not found.</returns>
    Task<ChatRoomResponse?> GetChatRoomByIdAsync(long chatRoomId, string userEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a chat room (soft delete by setting is_active = false).
    /// Only the creator can delete the room.
    /// </summary>
    /// <param name="chatRoomId">ID of the chat room.</param>
    /// <param name="userEmail">Email of the user requesting deletion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted, false if not authorized or not found.</returns>
    Task<bool> DeleteChatRoomAsync(long chatRoomId, string userEmail, CancellationToken cancellationToken = default);

    #endregion

    #region Participant Management

    /// <summary>
    /// Adds a user to a chat room. Checks the 30-user limit before joining.
    /// </summary>
    /// <param name="chatRoomId">ID of the chat room.</param>
    /// <param name="userEmail">Email of the user joining.</param>
    /// <param name="connectionId">SignalR connection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if joined successfully, false if room is full or doesn't exist.</returns>
    Task<bool> JoinChatRoomAsync(long chatRoomId, string userEmail, string connectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a user from a chat room (sets left_at timestamp).
    /// User must explicitly click "Leave" to trigger this.
    /// </summary>
    /// <param name="chatRoomId">ID of the chat room.</param>
    /// <param name="userEmail">Email of the user leaving.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if left successfully.</returns>
    Task<bool> LeaveChatRoomAsync(long chatRoomId, string userEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all participants in a chat room (active and historical).
    /// </summary>
    /// <param name="chatRoomId">ID of the chat room.</param>
    /// <param name="userEmail">Email of the requesting user.</param>
    /// <param name="activeOnly">If true, only returns currently connected users.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of participants with details.</returns>
    Task<List<ChatParticipantResponse>> GetChatParticipantsAsync(long chatRoomId, string userEmail, bool activeOnly = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of currently active participants in a room.
    /// Used to enforce the 30-user limit.
    /// </summary>
    /// <param name="chatRoomId">ID of the chat room.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of active participants.</returns>
    Task<int> GetActiveParticipantCountAsync(long chatRoomId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a user as connected in a chat room (sets is_currently_connected = true).
    /// Called when user's SignalR connection joins the room.
    /// </summary>
    /// <param name="chatRoomId">ID of the chat room.</param>
    /// <param name="userEmail">Email of the user.</param>
    /// <param name="connectionId">SignalR connection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkUserAsConnectedAsync(long chatRoomId, string userEmail, string connectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a user as disconnected in a chat room (sets is_currently_connected = false).
    /// Called when user's SignalR connection drops. Does NOT set left_at (user still in room).
    /// </summary>
    /// <param name="chatRoomId">ID of the chat room.</param>
    /// <param name="userEmail">Email of the user.</param>
    /// <param name="connectionId">SignalR connection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkUserAsDisconnectedAsync(long chatRoomId, string userEmail, string connectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all chat rooms a user is currently connected to via a specific connection ID.
    /// Used during OnDisconnectedAsync to clean up all rooms.
    /// </summary>
    /// <param name="userEmail">Email of the user.</param>
    /// <param name="connectionId">SignalR connection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of chat rooms the user is connected to.</returns>
    Task<List<ChatRoom>> GetUserActiveRoomsAsync(string userEmail, string connectionId, CancellationToken cancellationToken = default);

    #endregion

    #region Message Management

    /// <summary>
    /// Gets message history for a chat room.
    /// </summary>
    /// <param name="chatRoomId">ID of the chat room.</param>
    /// <param name="userEmail">Email of the requesting user.</param>
    /// <param name="limit">Maximum number of messages to return (default 100).</param>
    /// <param name="beforeMessageId">Optional: Get messages before this ID (for pagination).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of messages with sender information.</returns>
    Task<List<ChatMessageResponse>> GetChatMessagesAsync(long chatRoomId, string userEmail, int limit = 100, long? beforeMessageId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a user message to a chat room.
    /// </summary>
    /// <param name="chatRoomId">ID of the chat room.</param>
    /// <param name="userEmail">Email of the user sending the message.</param>
    /// <param name="content">Message content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created message with details.</returns>
    Task<ChatMessageResponse> AddUserMessageAsync(long chatRoomId, string userEmail, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an AI-generated message to a chat room.
    /// sender_email will be NULL, sender_type will be 'ai'.
    /// </summary>
    /// <param name="chatRoomId">ID of the chat room.</param>
    /// <param name="content">AI-generated message content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created AI message with details.</returns>
    Task<ChatMessageResponse> AddAiMessageAsync(long chatRoomId, string content, CancellationToken cancellationToken = default);

    #endregion

    #region Read Receipts

    /// <summary>
    /// Updates the last read message for a user in a chat room.
    /// </summary>
    /// <param name="chatRoomId">ID of the chat room.</param>
    /// <param name="userEmail">Email of the user.</param>
    /// <param name="lastReadMessageId">ID of the last message the user read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateReadReceiptAsync(long chatRoomId, string userEmail, long lastReadMessageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the number of unread messages for a user in a chat room.
    /// </summary>
    /// <param name="chatRoomId">ID of the chat room.</param>
    /// <param name="userEmail">Email of the user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Count of unread messages.</returns>
    Task<int> GetUnreadMessageCountAsync(long chatRoomId, string userEmail, CancellationToken cancellationToken = default);

    #endregion
}
