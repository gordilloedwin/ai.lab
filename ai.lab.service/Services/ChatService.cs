using ai.lab.service.Model.Database;
using ai.lab.service.Model.Outbound;
using ai.lab.service.Options;
using ai.lab.service.Services.Common;
using Dapper;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace ai.lab.service.Services;

/// <summary>
/// Service implementation for managing multi-user chat rooms with AI participant.
/// </summary>
public class ChatService : IChatService
{
    private readonly ILogger<ChatService> _logger;
    private readonly IOptionsMonitor<DatabaseOptions> _databaseOptions;

    public ChatService(ILogger<ChatService> logger, IOptionsMonitor<DatabaseOptions> databaseOptions)
    {
        _logger = logger;
        _databaseOptions = databaseOptions;
    }

    private string ConnectionString => _databaseOptions.CurrentValue.MariaDbConnectionString;

    #region Room Management

    public async Task<ChatRoomResponse> CreateChatRoomAsync(string userEmail, string title, string? aiModel = null, int? maxParticipants = null, CancellationToken cancellationToken = default)
    {
        try
        {
            const string sql = @"
                INSERT INTO chat_rooms (title, created_by_email, ai_model, max_participants)
                VALUES (@Title, @CreatedByEmail, @AiModel, @MaxParticipants);
                SELECT LAST_INSERT_ID();";

            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var roomId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                sql,
                new
                {
                    Title = title,
                    CreatedByEmail = userEmail,
                    AiModel = aiModel ?? "deepseek-coder:6.7b",
                    MaxParticipants = maxParticipants ?? 30
                },
                cancellationToken: cancellationToken));

            _logger.LogInformation("Created chat room {RoomId} by user {UserEmail}", roomId, userEmail);

            // Return the created room
            var room = await GetChatRoomByIdAsync(roomId, userEmail, cancellationToken);
            return room ?? throw new InvalidOperationException("Failed to retrieve created room");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create chat room for user {UserEmail}", userEmail);
            throw;
        }
    }

    public async Task<List<ChatRoomResponse>> GetUserChatRoomsAsync(string userEmail, CancellationToken cancellationToken = default)
    {
        try
        {
            const string sql = @"
                SELECT 
                    cr.id AS Id,
                    cr.title AS Title,
                    cr.created_by_email AS CreatedByEmail,
                    cu.name AS CreatedByName,
                    cr.ai_model AS AiModel,
                    cr.max_participants AS MaxParticipants,
                    cr.created_at AS CreatedAt,
                    cr.is_active AS IsActive,
                    COUNT(DISTINCT CASE WHEN cp2.is_currently_connected = TRUE AND cp2.left_at IS NULL THEN cp2.user_email END) AS CurrentParticipantCount,
                    COUNT(DISTINCT cm.id) AS TotalMessageCount,
                    MAX(cm.created_at) AS LastMessageAt,
                    COALESCE(
                        (SELECT COUNT(*) 
                         FROM chat_messages cm2 
                         LEFT JOIN chat_read_receipts crr2 ON crr2.chat_room_id = cm2.chat_room_id AND crr2.user_email = @UserEmail
                         WHERE cm2.chat_room_id = cr.id 
                           AND (crr2.last_read_message_id IS NULL OR cm2.id > crr2.last_read_message_id)), 
                        0
                    ) AS UnreadMessageCount
                FROM chat_rooms cr
                INNER JOIN chat_participants cp ON cr.id = cp.chat_room_id AND cp.user_email = @UserEmail AND cp.left_at IS NULL
                LEFT JOIN users cu ON cr.created_by_email = cu.email COLLATE utf8mb4_unicode_ci
                LEFT JOIN chat_participants cp2 ON cr.id = cp2.chat_room_id
                LEFT JOIN chat_messages cm ON cr.id = cm.chat_room_id
                WHERE cr.is_active = TRUE
                GROUP BY cr.id, cr.title, cr.created_by_email, cu.name, cr.ai_model, cr.max_participants, cr.created_at, cr.is_active;";

            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var rooms = await connection.QueryAsync<ChatRoomResponse>(new CommandDefinition(
                sql,
                new { UserEmail = userEmail },
                cancellationToken: cancellationToken));

            var roomList = rooms.ToList();

            // Set computed properties
            foreach (var room in roomList)
            {
                room.IsFull = room.CurrentParticipantCount >= room.MaxParticipants;
                room.IsUserInRoom = true; // By definition, these are user's rooms
            }

            return roomList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get chat rooms for user {UserEmail}", userEmail);
            throw;
        }
    }

    public async Task<List<ChatRoomResponse>> GetAllActiveChatRoomsAsync(string userEmail, CancellationToken cancellationToken = default)
    {
        try
        {
            const string sql = @"
                SELECT 
                    cr.id AS Id,
                    cr.title AS Title,
                    cr.created_by_email AS CreatedByEmail,
                    cu.name AS CreatedByName,
                    cr.ai_model AS AiModel,
                    cr.max_participants AS MaxParticipants,
                    cr.created_at AS CreatedAt,
                    cr.is_active AS IsActive,
                    COUNT(DISTINCT CASE WHEN cp.is_currently_connected = TRUE AND cp.left_at IS NULL THEN cp.user_email END) AS CurrentParticipantCount,
                    COUNT(DISTINCT cm.id) AS TotalMessageCount,
                    MAX(cm.created_at) AS LastMessageAt,
                    EXISTS(SELECT 1 FROM chat_participants cp_user WHERE cp_user.chat_room_id = cr.id AND cp_user.user_email = @UserEmail AND cp_user.left_at IS NULL) AS IsUserInRoom,
                    COALESCE(
                        (SELECT COUNT(*) 
                         FROM chat_messages cm2 
                         LEFT JOIN chat_read_receipts crr ON crr.chat_room_id = cm2.chat_room_id AND crr.user_email = @UserEmail
                         WHERE cm2.chat_room_id = cr.id 
                           AND (crr.last_read_message_id IS NULL OR cm2.id > crr.last_read_message_id)), 
                        0
                    ) AS UnreadMessageCount
                FROM chat_rooms cr
                LEFT JOIN users cu ON cr.created_by_email = cu.email COLLATE utf8mb4_unicode_ci
                LEFT JOIN chat_participants cp ON cr.id = cp.chat_room_id
                LEFT JOIN chat_messages cm ON cr.id = cm.chat_room_id
                WHERE cr.is_active = TRUE
                GROUP BY cr.id, cr.title, cr.created_by_email, cu.name, cr.ai_model, cr.max_participants, cr.created_at, cr.is_active;";

            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var rooms = await connection.QueryAsync<ChatRoomResponse>(new CommandDefinition(
                sql,
                new { UserEmail = userEmail },
                cancellationToken: cancellationToken));

            var roomList = rooms.ToList();

            // Set IsFull property
            foreach (var room in roomList)
            {
                room.IsFull = room.CurrentParticipantCount >= room.MaxParticipants;
            }

            return roomList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all active chat rooms for user {UserEmail}", userEmail);
            throw;
        }
    }

    public async Task<ChatRoomResponse?> GetChatRoomByIdAsync(long chatRoomId, string userEmail, CancellationToken cancellationToken = default)
    {
        try
        {
            const string sql = @"
                SELECT 
                    cr.id AS Id,
                    cr.title AS Title,
                    cr.created_by_email AS CreatedByEmail,
                    cu.name AS CreatedByName,
                    cr.ai_model AS AiModel,
                    cr.max_participants AS MaxParticipants,
                    cr.created_at AS CreatedAt,
                    cr.is_active AS IsActive,
                    COUNT(DISTINCT CASE WHEN cp.is_currently_connected = TRUE AND cp.left_at IS NULL THEN cp.user_email END) AS CurrentParticipantCount,
                    COUNT(DISTINCT cm.id) AS TotalMessageCount,
                    MAX(cm.created_at) AS LastMessageAt,
                    EXISTS(SELECT 1 FROM chat_participants cp_user WHERE cp_user.chat_room_id = cr.id AND cp_user.user_email = @UserEmail AND cp_user.left_at IS NULL) AS IsUserInRoom,
                    COALESCE(
                        (SELECT COUNT(*) 
                         FROM chat_messages cm2 
                         LEFT JOIN chat_read_receipts crr ON crr.chat_room_id = cm2.chat_room_id AND crr.user_email = @UserEmail
                         WHERE cm2.chat_room_id = cr.id 
                           AND (crr.last_read_message_id IS NULL OR cm2.id > crr.last_read_message_id)), 
                        0
                    ) AS UnreadMessageCount
                FROM chat_rooms cr
                LEFT JOIN users cu ON cr.created_by_email = cu.email COLLATE utf8mb4_unicode_ci
                LEFT JOIN chat_participants cp ON cr.id = cp.chat_room_id
                LEFT JOIN chat_messages cm ON cr.id = cm.chat_room_id
                WHERE cr.id = @ChatRoomId AND cr.is_active = TRUE
                GROUP BY cr.id, cr.title, cr.created_by_email, cu.name, cr.ai_model, cr.max_participants, cr.created_at, cr.is_active;";

            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var room = await connection.QuerySingleOrDefaultAsync<ChatRoomResponse>(new CommandDefinition(
                sql,
                new { ChatRoomId = chatRoomId, UserEmail = userEmail },
                cancellationToken: cancellationToken));

            if (room != null)
            {
                room.IsFull = room.CurrentParticipantCount >= room.MaxParticipants;
            }

            return room;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get chat room {RoomId} for user {UserEmail}", chatRoomId, userEmail);
            throw;
        }
    }

    public async Task<bool> DeleteChatRoomAsync(long chatRoomId, string userEmail, CancellationToken cancellationToken = default)
    {
        try
        {
            const string sql = @"
                UPDATE chat_rooms 
                SET is_active = FALSE 
                WHERE id = @ChatRoomId AND created_by_email = @UserEmail;";

            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { ChatRoomId = chatRoomId, UserEmail = userEmail },
                cancellationToken: cancellationToken));

            if (rowsAffected > 0)
            {
                _logger.LogInformation("Deleted chat room {RoomId} by user {UserEmail}", chatRoomId, userEmail);
                return true;
            }

            _logger.LogWarning("Failed to delete chat room {RoomId} - user {UserEmail} not authorized or room not found", chatRoomId, userEmail);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete chat room {RoomId} for user {UserEmail}", chatRoomId, userEmail);
            throw;
        }
    }

    #endregion

    #region Participant Management

    public async Task<bool> JoinChatRoomAsync(long chatRoomId, string userEmail, string connectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            // Check if room exists and get max participants
            const string checkRoomSql = @"
                SELECT max_participants 
                FROM chat_rooms 
                WHERE id = @ChatRoomId AND is_active = TRUE;";

            var maxParticipants = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
                checkRoomSql,
                new { ChatRoomId = chatRoomId },
                cancellationToken: cancellationToken));

            if (!maxParticipants.HasValue)
            {
                _logger.LogWarning("Chat room {RoomId} not found or inactive", chatRoomId);
                return false;
            }

            // Check current participant count
            var currentCount = await GetActiveParticipantCountAsync(chatRoomId, cancellationToken);

            if (currentCount >= maxParticipants.Value)
            {
                _logger.LogWarning("Chat room {RoomId} is full ({CurrentCount}/{MaxParticipants})", chatRoomId, currentCount, maxParticipants.Value);
                return false;
            }

            // Check if user already in room (not left)
            const string checkParticipantSql = @"
                SELECT id 
                FROM chat_participants 
                WHERE chat_room_id = @ChatRoomId AND user_email = @UserEmail AND left_at IS NULL;";

            var existingParticipant = await connection.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
                checkParticipantSql,
                new { ChatRoomId = chatRoomId, UserEmail = userEmail },
                cancellationToken: cancellationToken));

            if (existingParticipant.HasValue)
            {
                // User already in room, just update connection
                await MarkUserAsConnectedAsync(chatRoomId, userEmail, connectionId, cancellationToken);
                _logger.LogInformation("User {UserEmail} reconnected to chat room {RoomId}", userEmail, chatRoomId);
                return true;
            }

            // Add user to room
            const string insertSql = @"
                INSERT INTO chat_participants (chat_room_id, user_email, connection_id, is_currently_connected, last_seen_at)
                VALUES (@ChatRoomId, @UserEmail, @ConnectionId, TRUE, NOW());";

            await connection.ExecuteAsync(new CommandDefinition(
                insertSql,
                new { ChatRoomId = chatRoomId, UserEmail = userEmail, ConnectionId = connectionId },
                cancellationToken: cancellationToken));

            _logger.LogInformation("User {UserEmail} joined chat room {RoomId}", userEmail, chatRoomId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join chat room {RoomId} for user {UserEmail}", chatRoomId, userEmail);
            throw;
        }
    }

    public async Task<bool> LeaveChatRoomAsync(long chatRoomId, string userEmail, CancellationToken cancellationToken = default)
    {
        try
        {
            const string sql = @"
                UPDATE chat_participants 
                SET left_at = NOW(), is_currently_connected = FALSE, connection_id = NULL
                WHERE chat_room_id = @ChatRoomId AND user_email = @UserEmail AND left_at IS NULL;";

            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { ChatRoomId = chatRoomId, UserEmail = userEmail },
                cancellationToken: cancellationToken));

            if (rowsAffected > 0)
            {
                _logger.LogInformation("User {UserEmail} left chat room {RoomId}", userEmail, chatRoomId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to leave chat room {RoomId} for user {UserEmail}", chatRoomId, userEmail);
            throw;
        }
    }

    public async Task<List<ChatParticipantResponse>> GetChatParticipantsAsync(long chatRoomId, string userEmail, bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var sql = @"
                SELECT 
                    cp.user_email AS UserEmail,
                    u.name AS UserName,
                    u.avatar_uri AS AvatarUri,
                    cp.joined_at AS JoinedAt,
                    cp.left_at AS LeftAt,
                    cp.is_currently_connected AS IsCurrentlyConnected,
                    cp.last_seen_at AS LastSeenAt,
                    TIMESTAMPDIFF(SECOND, cp.joined_at, COALESCE(cp.left_at, NOW())) AS TimeInRoomSeconds
                FROM chat_participants cp
                JOIN users u ON cp.user_email = u.email COLLATE utf8mb4_unicode_ci
                WHERE cp.chat_room_id = @ChatRoomId";

            if (activeOnly)
            {
                sql += " AND cp.is_currently_connected = TRUE AND cp.left_at IS NULL";
            }

            sql += " ORDER BY cp.joined_at DESC;";

            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var participants = await connection.QueryAsync<ChatParticipantResponse>(new CommandDefinition(
                sql,
                new { ChatRoomId = chatRoomId },
                cancellationToken: cancellationToken));

            var participantList = participants.ToList();

            // Mark current user
            foreach (var participant in participantList)
            {
                participant.IsCurrentUser = participant.UserEmail.Equals(userEmail, StringComparison.OrdinalIgnoreCase);
            }

            return participantList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get participants for chat room {RoomId}", chatRoomId);
            throw;
        }
    }

    public async Task<int> GetActiveParticipantCountAsync(long chatRoomId, CancellationToken cancellationToken = default)
    {
        try
        {
            const string sql = @"
                SELECT COUNT(DISTINCT user_email)
                FROM chat_participants
                WHERE chat_room_id = @ChatRoomId AND is_currently_connected = TRUE AND left_at IS NULL;";

            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                sql,
                new { ChatRoomId = chatRoomId },
                cancellationToken: cancellationToken));

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active participant count for chat room {RoomId}", chatRoomId);
            throw;
        }
    }

    public async Task MarkUserAsConnectedAsync(long chatRoomId, string userEmail, string connectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            const string sql = @"
                UPDATE chat_participants 
                SET is_currently_connected = TRUE, connection_id = @ConnectionId, last_seen_at = NOW()
                WHERE chat_room_id = @ChatRoomId AND user_email = @UserEmail AND left_at IS NULL;";

            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { ChatRoomId = chatRoomId, UserEmail = userEmail, ConnectionId = connectionId },
                cancellationToken: cancellationToken));

            _logger.LogInformation("Marked user {UserEmail} as connected in chat room {RoomId}", userEmail, chatRoomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark user {UserEmail} as connected in chat room {RoomId}", userEmail, chatRoomId);
            throw;
        }
    }

    public async Task MarkUserAsDisconnectedAsync(long chatRoomId, string userEmail, string connectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            const string sql = @"
                UPDATE chat_participants 
                SET is_currently_connected = FALSE, last_seen_at = NOW()
                WHERE chat_room_id = @ChatRoomId AND user_email = @UserEmail AND connection_id = @ConnectionId AND left_at IS NULL;";

            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { ChatRoomId = chatRoomId, UserEmail = userEmail, ConnectionId = connectionId },
                cancellationToken: cancellationToken));

            _logger.LogInformation("Marked user {UserEmail} as disconnected in chat room {RoomId}", userEmail, chatRoomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark user {UserEmail} as disconnected in chat room {RoomId}", userEmail, chatRoomId);
            throw;
        }
    }

    public async Task<List<ChatRoom>> GetUserActiveRoomsAsync(string userEmail, string connectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            const string sql = @"
                SELECT 
                    cr.id AS Id,
                    cr.title AS Title,
                    cr.created_by_email AS CreatedByEmail,
                    cr.ai_model AS AiModel,
                    cr.max_participants AS MaxParticipants,
                    cr.created_at AS CreatedAt,
                    cr.updated_at AS UpdatedAt,
                    cr.is_active AS IsActive
                FROM chat_rooms cr
                INNER JOIN chat_participants cp ON cr.id = cp.chat_room_id
                WHERE cp.user_email = @UserEmail 
                  AND cp.connection_id = @ConnectionId 
                  AND cp.is_currently_connected = TRUE 
                  AND cp.left_at IS NULL;";

            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var rooms = await connection.QueryAsync<ChatRoom>(new CommandDefinition(
                sql,
                new { UserEmail = userEmail, ConnectionId = connectionId },
                cancellationToken: cancellationToken));

            return rooms.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active rooms for user {UserEmail} with connection {ConnectionId}", userEmail, connectionId);
            throw;
        }
    }

    #endregion

    #region Message Management

    public async Task<List<ChatMessageResponse>> GetChatMessagesAsync(long chatRoomId, string userEmail, int limit = 100, long? beforeMessageId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var sql = @"
                SELECT 
                    cm.id AS Id,
                    cm.chat_room_id AS ChatRoomId,
                    cm.sender_email AS SenderEmail,
                    u.name AS SenderName,
                    u.avatar_uri AS SenderAvatarUri,
                    cm.sender_type AS SenderType,
                    cm.content AS Content,
                    cm.created_at AS CreatedAt
                FROM chat_messages cm
                LEFT JOIN users u ON cm.sender_email = u.email COLLATE utf8mb4_unicode_ci
                WHERE cm.chat_room_id = @ChatRoomId";

            if (beforeMessageId.HasValue)
            {
                sql += " AND cm.id < @BeforeMessageId";
            }

            sql += " ORDER BY cm.id DESC LIMIT @Limit;";

            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var messages = await connection.QueryAsync<ChatMessageResponse>(new CommandDefinition(
                sql,
                new { ChatRoomId = chatRoomId, BeforeMessageId = beforeMessageId, Limit = limit },
                cancellationToken: cancellationToken));

            var messageList = messages.Reverse().ToList(); // Reverse to get chronological order

            // Mark own messages and set AI name for AI messages
            foreach (var message in messageList)
            {
                message.IsOwnMessage = message.SenderEmail?.Equals(userEmail, StringComparison.OrdinalIgnoreCase) ?? false;
                
                if (message.SenderType == "ai")
                {
                    message.SenderName = "AI Assistant";
                    message.SenderAvatarUri = null;
                }
            }

            return messageList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get messages for chat room {RoomId}", chatRoomId);
            throw;
        }
    }

    public async Task<ChatMessageResponse> AddUserMessageAsync(long chatRoomId, string userEmail, string content, CancellationToken cancellationToken = default)
    {
        try
        {
            const string sql = @"
                INSERT INTO chat_messages (chat_room_id, sender_email, sender_type, content)
                VALUES (@ChatRoomId, @SenderEmail, 'user', @Content);
                SELECT LAST_INSERT_ID();";

            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var messageId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                sql,
                new { ChatRoomId = chatRoomId, SenderEmail = userEmail, Content = content },
                cancellationToken: cancellationToken));

            _logger.LogInformation("Added user message {MessageId} in chat room {RoomId} by {UserEmail}", messageId, chatRoomId, userEmail);

            // Retrieve the created message with user details
            const string selectSql = @"
                SELECT 
                    cm.id AS Id,
                    cm.chat_room_id AS ChatRoomId,
                    cm.sender_email AS SenderEmail,
                    u.name AS SenderName,
                    u.avatar_uri AS SenderAvatarUri,
                    cm.sender_type AS SenderType,
                    cm.content AS Content,
                    cm.created_at AS CreatedAt
                FROM chat_messages cm
                JOIN users u ON cm.sender_email = u.email COLLATE utf8mb4_unicode_ci
                WHERE cm.id = @MessageId;";

            var message = await connection.QuerySingleAsync<ChatMessageResponse>(new CommandDefinition(
                selectSql,
                new { MessageId = messageId },
                cancellationToken: cancellationToken));

            message.IsOwnMessage = true;
            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add user message in chat room {RoomId} by {UserEmail}", chatRoomId, userEmail);
            throw;
        }
    }

    public async Task<ChatMessageResponse> AddAiMessageAsync(long chatRoomId, string content, CancellationToken cancellationToken = default)
    {
        try
        {
            const string sql = @"
                INSERT INTO chat_messages (chat_room_id, sender_email, sender_type, content)
                VALUES (@ChatRoomId, NULL, 'ai', @Content);
                SELECT LAST_INSERT_ID();";

            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var messageId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                sql,
                new { ChatRoomId = chatRoomId, Content = content },
                cancellationToken: cancellationToken));

            _logger.LogInformation("Added AI message {MessageId} in chat room {RoomId}", messageId, chatRoomId);

            // Return the AI message
            return new ChatMessageResponse
            {
                Id = messageId,
                ChatRoomId = chatRoomId,
                SenderEmail = null,
                SenderName = "AI Assistant",
                SenderAvatarUri = null,
                SenderType = "ai",
                Content = content,
                CreatedAt = DateTime.UtcNow,
                IsOwnMessage = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add AI message in chat room {RoomId}", chatRoomId);
            throw;
        }
    }

    #endregion

    #region Read Receipts

    public async Task UpdateReadReceiptAsync(long chatRoomId, string userEmail, long lastReadMessageId, CancellationToken cancellationToken = default)
    {
        try
        {
            const string sql = @"
                INSERT INTO chat_read_receipts (chat_room_id, user_email, last_read_message_id)
                VALUES (@ChatRoomId, @UserEmail, @LastReadMessageId)
                ON DUPLICATE KEY UPDATE last_read_message_id = @LastReadMessageId, read_at = NOW();";

            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { ChatRoomId = chatRoomId, UserEmail = userEmail, LastReadMessageId = lastReadMessageId },
                cancellationToken: cancellationToken));

            _logger.LogInformation("Updated read receipt for user {UserEmail} in chat room {RoomId} to message {MessageId}", userEmail, chatRoomId, lastReadMessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update read receipt for user {UserEmail} in chat room {RoomId}", userEmail, chatRoomId);
            throw;
        }
    }

    public async Task<int> GetUnreadMessageCountAsync(long chatRoomId, string userEmail, CancellationToken cancellationToken = default)
    {
        try
        {
            const string sql = @"
                SELECT COUNT(*)
                FROM chat_messages cm
                LEFT JOIN chat_read_receipts crr ON crr.chat_room_id = cm.chat_room_id AND crr.user_email = @UserEmail
                WHERE cm.chat_room_id = @ChatRoomId 
                  AND (crr.last_read_message_id IS NULL OR cm.id > crr.last_read_message_id);";

            using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                sql,
                new { ChatRoomId = chatRoomId, UserEmail = userEmail },
                cancellationToken: cancellationToken));

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get unread message count for user {UserEmail} in chat room {RoomId}", userEmail, chatRoomId);
            throw;
        }
    }

    #endregion
}
