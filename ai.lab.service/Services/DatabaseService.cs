using ai.lab.service.Helpers;
using ai.lab.service.Model.Database;
using ai.lab.service.Model.Outbound;
using ai.lab.service.Options;
using ai.lab.service.Services.Common;
using Dapper;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace ai.lab.service.Services;

public class DatabaseService(IOptionsMonitor<DatabaseOptions> options, ILogger<DatabaseService> logger) : IDatabaseService
{
    #region Users and Chunks

    public async Task TestDataBaseAccessAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Database connection string is not configured.");
            }

            using var connection = new MySqlConnection(connectionString);
            connection.OpenAsync(cancellationToken).Wait(cancellationToken);
            using var command = new MySqlCommand("SELECT 1", connection);
            var result = await command.ExecuteScalarAsync(cancellationToken);

            if (result == null || Convert.ToInt32(result) != 1)
            {
                throw new InvalidOperationException("Database test query did not return the expected result.");
            }

            logger.LogInformation("Successfully connected to the database and executed test query.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to access the database.");
            throw;
        }
    }

    public async Task<long> InsertChunkAsync(MariaDbChunkEmbedding chunk, CancellationToken cancellationToken)
    {
        try
        {
            const string sql = @"
            INSERT INTO chat_chunk_embeddings
            (
                model,
                chunk_id,
                chunk_text,
                file_name,
                tags,
                embedding
            ) VALUES 
            (
                @Model,
                @ChunkId,
                @ChunkText,
                @FileName,
                @Tags,
                @Embedding
            )
            ON DUPLICATE KEY UPDATE
                model = VALUES(model),
                chunk_text = VALUES(chunk_text),
                file_name = VALUES(file_name),
                tags = VALUES(tags),
                embedding = VALUES(embedding),
                updated_at = CURRENT_TIMESTAMP;";

            SqlMapper.AddTypeHandler(new VectorHandler());
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
            connection.OpenAsync(cancellationToken).Wait(cancellationToken);
            var id = await connection.ExecuteScalarAsync<long>(sql, chunk);
            return id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to insert chunk embedding into the database.");
            throw;
        }
    }

    public async Task<bool> DeleteOldChunksAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            const string sql = @"
            DELETE FROM chat_chunk_embeddings 
            WHERE file_name = @FileName;";
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
            connection.OpenAsync(cancellationToken).Wait(cancellationToken);
            var rowsAffected = await connection.ExecuteAsync(sql, new { FileName = filePath });
            return !string.IsNullOrEmpty(filePath) || rowsAffected > 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete old chunks from the database for file {FileName}.", filePath);
            throw;
        }
    }

    public async Task<List<MariaDbChunkEmbedding>> GetRelevantChunksAsync(string model, float[] embedding, int topK, CancellationToken cancellationToken)
    {
        try
        {
            const string sql = @"
            SELECT 
                id AS Id,
                model AS Model,
                chunk_id AS ChunkId,
                chunk_text AS ChunkText,
                file_name AS FileName,
                tags AS Tags,
                embedding AS Embedding,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt,
                (1 - (DOT_PRODUCT(embedding, @Embedding) / (VECTOR_NORM(embedding) * VECTOR_NORM(@Embedding)))) AS distance
            FROM chat_chunk_embeddings
            WHERE model = @Model
            ORDER BY distance ASC
            LIMIT @TopK;";

            SqlMapper.AddTypeHandler(new VectorHandler());
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
            connection.OpenAsync(cancellationToken).Wait(cancellationToken);
            var chunks = await connection.QueryAsync<MariaDbChunkEmbedding>(sql, new { Model = model, Embedding = embedding, TopK = topK });
            return chunks.ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve relevant chunks from the database.");
            throw;
        }
    }

    public async Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Database connection string is not configured.");
            }

            const string sql = @"
            SELECT
                id AS Id,
                email AS Email,
                name AS Name,
                password_hash AS PasswordHash,
                avatar_uri AS AvatarUri,
                is_admin AS IsAdmin,
                last_seen AS LastSeen,
                created_at AS CreatedAt,
                context_json AS ContextJson
            FROM users
            WHERE email = @Email
            LIMIT 1;";

            using var connection = new MySqlConnection(connectionString);
            connection.OpenAsync(cancellationToken).Wait(cancellationToken);
            var user = await connection.QuerySingleOrDefaultAsync<User>(sql, new { Email = email });
            return user;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve user by email from the database.");
            throw;
        }
    }

    public async Task<bool> AddUserAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Database connection string is not configured.");
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return false;
            }

            // Insert only if email is not null and not already present
            const string sql = @"
            INSERT INTO users 
            (
                email,
                name,
                password_hash,
                avatar_uri,
                is_admin,
                last_seen,
                created_at,
                context_json
            )
            SELECT 
                @Email,
                @Name,
                @PasswordHash,
                @AvatarUri,
                @IsAdmin,                
                @LastSeen,
                @CreatedAt,
                @ContextJson
            WHERE 
                @Email IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM users WHERE email = @Email);";

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(sql, user, cancellationToken: cancellationToken));
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add user to the database.");
            throw;
        }
    }

    public async Task UpdateUserLastSeenAsync(string email, DateTime lastSeen, List<UserChatContext> context, CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Database connection string is not configured.");
            }
            const string sql = @"
            UPDATE 
                users
            SET 
                last_seen = @LastSeen,
                context_json = @ContextJson
            WHERE 
                email = @Email;";

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(sql,
                new { Email = email, LastSeen = lastSeen, ContextJson = System.Text.Json.JsonSerializer.Serialize(context) }, cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update user's last seen in the database.");
            throw;
        }
    }

    #endregion

    #region Room Management

    public async Task<ChatRoomResponse> CreateChatRoomAsync(string userEmail, string title, string? aiModel = null, int? maxParticipants = null, CancellationToken cancellationToken = default)
    {
        try
        {
            const string sql = @"
                INSERT INTO chat_rooms (title, created_by_email, ai_model, max_participants)
                VALUES (@Title, @CreatedByEmail, @AiModel, @MaxParticipants);
                SELECT LAST_INSERT_ID();";
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var roomId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                sql,
                new
                {
                    Title = title,
                    AiModel = aiModel,
                    CreatedByEmail = userEmail,
                    MaxParticipants = maxParticipants ?? 30
                },
                cancellationToken: cancellationToken));

            logger.LogInformation("Created chat room {RoomId} by user {UserEmail}", roomId, userEmail);

            // Return the created room
            var room = await GetChatRoomByIdAsync(roomId, userEmail, cancellationToken);
            return room ?? throw new InvalidOperationException("Failed to retrieve created room");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create chat room for user {UserEmail}", userEmail);
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
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
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
            logger.LogError(ex, "Failed to get chat rooms for user {UserEmail}", userEmail);
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
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
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
            logger.LogError(ex, "Failed to get all active chat rooms for user {UserEmail}", userEmail);
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
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
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
            logger.LogError(ex, "Failed to get chat room {RoomId} for user {UserEmail}", chatRoomId, userEmail);
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
                WHERE id = @ChatRoomId 
                  AND (
                        created_by_email = @UserEmail 
                        OR (SELECT is_admin FROM users WHERE email = @UserEmail) = 1
                  );";
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { ChatRoomId = chatRoomId, UserEmail = userEmail },
                cancellationToken: cancellationToken));

            var success = rowsAffected > 0;
            if (success)
            {
                logger.LogInformation("Deleted chat room {RoomId} by user {UserEmail} (creator or admin)", chatRoomId, userEmail);
            }
            else
            {
                logger.LogWarning("Failed to delete chat room {RoomId} - user {UserEmail} not authorized or room not found", chatRoomId, userEmail);
            }
            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete chat room {RoomId} for user {UserEmail}", chatRoomId, userEmail);
            throw;
        }
    }

    #endregion

    #region Participant Management

    public async Task<bool> JoinChatRoomAsync(long chatRoomId, string userEmail, string connectionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
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
                logger.LogWarning("Chat room {RoomId} not found or inactive", chatRoomId);
                return false;
            }

            // Check current participant count
            var currentCount = await GetActiveParticipantCountAsync(chatRoomId, cancellationToken);

            if (currentCount >= maxParticipants.Value)
            {
                logger.LogWarning("Chat room {RoomId} is full ({CurrentCount}/{MaxParticipants})", chatRoomId, currentCount, maxParticipants.Value);
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
                logger.LogInformation("User {UserEmail} reconnected to chat room {RoomId}", userEmail, chatRoomId);
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

            logger.LogInformation("User {UserEmail} joined chat room {RoomId}", userEmail, chatRoomId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to join chat room {RoomId} for user {UserEmail}", chatRoomId, userEmail);
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
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { ChatRoomId = chatRoomId, UserEmail = userEmail },
                cancellationToken: cancellationToken));

            if (rowsAffected > 0)
            {
                logger.LogInformation("User {UserEmail} left chat room {RoomId}", userEmail, chatRoomId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to leave chat room {RoomId} for user {UserEmail}", chatRoomId, userEmail);
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
                    TIMESTAMPDIFF(SECOND, cp.joined_at, COALESCE(cp.left_at, NOW())) AS TimeInRoomSeconds,
                    crr.last_read_message_id AS LastReadMessageId
                FROM chat_participants cp
                JOIN users u ON cp.user_email = u.email COLLATE utf8mb4_unicode_ci
                LEFT JOIN chat_read_receipts crr ON crr.chat_room_id = cp.chat_room_id AND crr.user_email = cp.user_email
                WHERE cp.chat_room_id = @ChatRoomId";

            if (activeOnly)
            {
                sql += " AND cp.is_currently_connected = TRUE AND cp.left_at IS NULL";
            }

            sql += " ORDER BY cp.joined_at DESC;";
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
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
            logger.LogError(ex, "Failed to get participants for chat room {RoomId}", chatRoomId);
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
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                sql,
                new { ChatRoomId = chatRoomId },
                cancellationToken: cancellationToken));

            return count;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get active participant count for chat room {RoomId}", chatRoomId);
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
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { ChatRoomId = chatRoomId, UserEmail = userEmail, ConnectionId = connectionId },
                cancellationToken: cancellationToken));

            logger.LogInformation("Marked user {UserEmail} as connected in chat room {RoomId}", userEmail, chatRoomId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to mark user {UserEmail} as connected in chat room {RoomId}", userEmail, chatRoomId);
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
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { ChatRoomId = chatRoomId, UserEmail = userEmail, ConnectionId = connectionId },
                cancellationToken: cancellationToken));

            logger.LogInformation("Marked user {UserEmail} as disconnected in chat room {RoomId}", userEmail, chatRoomId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to mark user {UserEmail} as disconnected in chat room {RoomId}", userEmail, chatRoomId);
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
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var rooms = await connection.QueryAsync<ChatRoom>(new CommandDefinition(
                sql,
                new { UserEmail = userEmail, ConnectionId = connectionId },
                cancellationToken: cancellationToken));

            return rooms.ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get active rooms for user {UserEmail} with connection {ConnectionId}", userEmail, connectionId);
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
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
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
            logger.LogError(ex, "Failed to get messages for chat room {RoomId}", chatRoomId);
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
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var messageId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                sql,
                new { ChatRoomId = chatRoomId, SenderEmail = userEmail, Content = content },
                cancellationToken: cancellationToken));

            logger.LogInformation("Added user message {MessageId} in chat room {RoomId} by {UserEmail}", messageId, chatRoomId, userEmail);

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
            logger.LogError(ex, "Failed to add user message in chat room {RoomId} by {UserEmail}", chatRoomId, userEmail);
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
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var messageId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                sql,
                new { ChatRoomId = chatRoomId, Content = content },
                cancellationToken: cancellationToken));

            logger.LogInformation("Added AI message {MessageId} in chat room {RoomId}", messageId, chatRoomId);

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
            logger.LogError(ex, "Failed to add AI message in chat room {RoomId}", chatRoomId);
            throw;
        }
    }

    public async Task<ChatMessageResponse?> UpdateUserMessageAsync(long chatRoomId, long messageId, string userEmail, string newContent, CancellationToken cancellationToken = default)
    {
        try
        {
            // Verify ownership or admin
            const string ownershipSql = @"
                SELECT cm.id AS Id, cm.chat_room_id AS ChatRoomId, cm.sender_email AS SenderEmail, u.name AS SenderName, u.avatar_uri AS SenderAvatarUri,
                       cm.sender_type AS SenderType, cm.content AS Content, cm.created_at AS CreatedAt,
                       (SELECT is_admin FROM users WHERE email = @UserEmail) AS IsAdmin
                FROM chat_messages cm
                LEFT JOIN users u ON cm.sender_email = u.email COLLATE utf8mb4_unicode_ci
                WHERE cm.id = @MessageId AND cm.chat_room_id = @ChatRoomId LIMIT 1;";

            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            var record = await connection.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(ownershipSql, new { MessageId = messageId, ChatRoomId = chatRoomId, UserEmail = userEmail }, cancellationToken: cancellationToken));
            if (record == null) return null;
            bool isAdmin = (record.IsAdmin ?? 0) == 1;
            string senderEmail = record.SenderEmail;
            string senderType = record.SenderType;
            if (senderType != "user") return null; // only user messages editable
            if (!isAdmin && !string.Equals(senderEmail, userEmail, StringComparison.OrdinalIgnoreCase)) return null;

            const string updateSql = @"UPDATE chat_messages SET content = @NewContent WHERE id = @MessageId AND chat_room_id = @ChatRoomId LIMIT 1;";
            await connection.ExecuteAsync(new CommandDefinition(updateSql, new { NewContent = newContent, MessageId = messageId, ChatRoomId = chatRoomId }, cancellationToken: cancellationToken));

            // Re-select updated row
            const string selectSql = @"
                SELECT cm.id AS Id, cm.chat_room_id AS ChatRoomId, cm.sender_email AS SenderEmail, u.name AS SenderName, u.avatar_uri AS SenderAvatarUri,
                       cm.sender_type AS SenderType, cm.content AS Content, cm.created_at AS CreatedAt
                FROM chat_messages cm
                LEFT JOIN users u ON cm.sender_email = u.email COLLATE utf8mb4_unicode_ci
                WHERE cm.id = @MessageId;";
            var updated = await connection.QuerySingleAsync<ChatMessageResponse>(new CommandDefinition(selectSql, new { MessageId = messageId }, cancellationToken: cancellationToken));
            updated.IsOwnMessage = true;
            return updated;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update user message {MessageId} in chat room {RoomId} by {UserEmail}", messageId, chatRoomId, userEmail);
            return null;
        }
    }

    public async Task<ChatMessageResponse?> SoftDeleteUserMessageAsync(long chatRoomId, long messageId, string userEmail, CancellationToken cancellationToken = default)
    {
        try
        {
            const string ownershipSql = @"
                SELECT cm.id AS Id, cm.chat_room_id AS ChatRoomId, cm.sender_email AS SenderEmail, u.name AS SenderName, u.avatar_uri AS SenderAvatarUri,
                       cm.sender_type AS SenderType, cm.content AS Content, cm.created_at AS CreatedAt,
                       (SELECT is_admin FROM users WHERE email = @UserEmail) AS IsAdmin
                FROM chat_messages cm
                LEFT JOIN users u ON cm.sender_email = u.email COLLATE utf8mb4_unicode_ci
                WHERE cm.id = @MessageId AND cm.chat_room_id = @ChatRoomId LIMIT 1;";

            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            var record = await connection.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(ownershipSql, new { MessageId = messageId, ChatRoomId = chatRoomId, UserEmail = userEmail }, cancellationToken: cancellationToken));
            if (record == null) return null;
            bool isAdmin = (record.IsAdmin ?? 0) == 1;
            string senderEmail = record.SenderEmail;
            string senderType = record.SenderType;
            if (senderType != "user") return null; // only user messages deletable by user
            if (!isAdmin && !string.Equals(senderEmail, userEmail, StringComparison.OrdinalIgnoreCase)) return null;

            const string updateSql = @"UPDATE chat_messages SET content = '[[deleted]]' WHERE id = @MessageId AND chat_room_id = @ChatRoomId LIMIT 1;";
            await connection.ExecuteAsync(new CommandDefinition(updateSql, new { MessageId = messageId, ChatRoomId = chatRoomId }, cancellationToken: cancellationToken));

            const string selectSql = @"
                SELECT cm.id AS Id, cm.chat_room_id AS ChatRoomId, cm.sender_email AS SenderEmail, u.name AS SenderName, u.avatar_uri AS SenderAvatarUri,
                       cm.sender_type AS SenderType, cm.content AS Content, cm.created_at AS CreatedAt
                FROM chat_messages cm
                LEFT JOIN users u ON cm.sender_email = u.email COLLATE utf8mb4_unicode_ci
                WHERE cm.id = @MessageId;";
            var deleted = await connection.QuerySingleAsync<ChatMessageResponse>(new CommandDefinition(selectSql, new { MessageId = messageId }, cancellationToken: cancellationToken));
            deleted.IsOwnMessage = true;
            return deleted;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to soft delete user message {MessageId} in chat room {RoomId} by {UserEmail}", messageId, chatRoomId, userEmail);
            return null;
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
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { ChatRoomId = chatRoomId, UserEmail = userEmail, LastReadMessageId = lastReadMessageId },
                cancellationToken: cancellationToken));

            logger.LogInformation("Updated read receipt for user {UserEmail} in chat room {RoomId} to message {MessageId}", userEmail, chatRoomId, lastReadMessageId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update read receipt for user {UserEmail} in chat room {RoomId}", userEmail, chatRoomId);
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
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                sql,
                new { ChatRoomId = chatRoomId, UserEmail = userEmail },
                cancellationToken: cancellationToken));

            return count;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get unread message count for user {UserEmail} in chat room {RoomId}", userEmail, chatRoomId);
            throw;
        }
    }

    #endregion
}
