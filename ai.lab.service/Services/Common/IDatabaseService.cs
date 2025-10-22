using ai.lab.service.Model.Database;
using ai.lab.service.Model.Outbound;

namespace ai.lab.service.Services.Common;

public interface IDatabaseService
{
    /// <summary>
    /// Asynchronously verifies connectivity and access to the database.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the database access test operation.</param>
    /// <returns>A task that represents the asynchronous operation of testing database access.</returns>
    Task TestDataBaseAccessAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously inserts a chunk embedding into the MariaDB database.
    /// </summary>
    /// <param name="chunk">The chunk embedding to insert. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the identifier of the newly inserted
    /// chunk.</returns>
    Task<long> InsertChunkAsync(MariaDbChunkEmbedding chunk, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes old data chunks associated with the specified file from the MariaDB database asynchronously.
    /// </summary>
    /// <param name="filePath">The full path of the file whose old chunks should be deleted. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the delete operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if any old chunks
    /// were deleted; otherwise, <see langword="false"/>.</returns>
    Task<bool> DeleteOldChunksAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether the hash for the specified file chunk has already been processed asynchronously.
    /// </summary>
    /// <param name="chunkId">The unique identifier of the file chunk to validate.</param>
    /// <param name="file">The name or path of the file containing the chunk to check.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the hash for the
    /// specified chunk has already been processed; otherwise, <see langword="false"/>.</returns>
    Task<bool> ValidateHashAlreadyProcessedAsync(string chunkId, string file, CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronously retrieves the most relevant data chunks based on the provided embedding vector.
    /// </summary>
    /// <param name="model">The model name to filter chunks by.</param>
    /// <param name="embedding">The embedding vector to use for retrieving relevant chunks. Cannot be null.</param>
    /// <param name="topK">The maximum number of relevant chunks to retrieve.</param>
    /// <param name="filterTags">Optional list of tags to filter chunks. Only chunks containing at least one of these tags will be returned.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of the most relevant data chunks.</returns>
    Task<List<MariaDbChunkEmbedding>> GetRelevantChunksAsync
        (string model, float[] embedding, int topK, List<string>? filterTags = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a user by their email address.
    /// </summary>
    /// <param name="email">The email address of the user to retrieve. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the user associated with the
    /// specified email address, or null if no user is found.</returns>
    Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronously adds a new user to the system.
    /// </summary>
    /// <param name="user">The user to add. Cannot be null. The user's properties must meet any required validation criteria.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the add operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the user was
    /// added successfully; otherwise, <see langword="false"/>.</returns>
    Task<bool> AddUserAsync(User user, CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronously updates the last seen timestamp for the specified user and associates it with the provided
    /// context identifiers.
    /// </summary>
    /// <param name="email">The email address of the user whose last seen information is to be updated. Cannot be null or empty.</param>
    /// <param name="lastSeen">The date and time, in UTC, representing when the user was last seen.</param>
    /// <param name="context">A list of context identifiers to associate with the user's last seen update. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    Task UpdateUserLastSeenAsync(string email, DateTime lastSeen, List<UserChatContext> context, CancellationToken cancellationToken);

    #region Room Management

    /// <summary>
    /// Creates a new chat room.
    /// </summary>
    /// <param name="userEmail">Email of the user creating the room.</param>
    /// <param name="title">Title of the chat room.</param>
    /// <param name="aiModel">Optional AI model to use.</param>
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

    /// <summary>
    /// Updates existing user message content if user owns the message or is admin.
    /// </summary>
    Task<ChatMessageResponse?> UpdateUserMessageAsync(long chatRoomId, long messageId, string userEmail, string newContent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes an existing user message (replace content) if user owns message or is admin.
    /// </summary>
    Task<ChatMessageResponse?> SoftDeleteUserMessageAsync(long chatRoomId, long messageId, string userEmail, CancellationToken cancellationToken = default);

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
