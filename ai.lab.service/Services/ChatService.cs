using ai.lab.service.Model.Database;
using ai.lab.service.Model.Outbound;
using ai.lab.service.Services.Common;

namespace ai.lab.service.Services;

/// <summary>
/// Service implementation for managing multi-user chat rooms with AI participant.
/// </summary>
public class ChatService(ILogger<ChatService> logger, IOllamaClient ollamaClient, IDatabaseService databaseService) : IChatService
{
    // Simple in-memory cache for AI models with TTL
    private static List<string>? _cachedModels;
    private static DateTime _cachedModelsExpiresUtc = DateTime.MinValue;
    private static readonly TimeSpan _modelsTtl = TimeSpan.FromMinutes(5);
    private static readonly object _modelsLock = new();

    public async Task<List<string>> GetAvailableAiModels(CancellationToken cancellationToken = default)
    {
        // Fast path if cache valid
        if (_cachedModels is not null && DateTime.UtcNow < _cachedModelsExpiresUtc)
        {
            return _cachedModels;
        }

        // Fetch + update cache (single thread populates)
        try
        {
            var models = await ollamaClient.GetAvailableAiModels(cancellationToken);
            lock (_modelsLock)
            {
                _cachedModels = models;
                _cachedModelsExpiresUtc = DateTime.UtcNow.Add(_modelsTtl);
            }
            return models;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to refresh AI models list; serving stale cache if available.");
            if (_cachedModels is not null)
            {
                return _cachedModels; // stale fallback
            }
            // If no cache at all, rethrow to surface error
            throw;
        }
    }

    public async Task<ChatMessageResponse> AddAiMessageAsync(long chatRoomId, string content, CancellationToken cancellationToken = default)
    {
        try
        {
            return await databaseService.AddAiMessageAsync(chatRoomId, content, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding AI message to chat room {ChatRoomId}", chatRoomId);
            throw;
        }
    }

    public async Task<ChatMessageResponse?> UpdateMessageContentAsync(long chatRoomId, long messageId, string userEmail, string newContent, CancellationToken cancellationToken = default)
    {
        try
        {
            return await databaseService.UpdateUserMessageAsync(chatRoomId, messageId, userEmail, newContent, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update message {MessageId} in chat room {ChatRoomId} by {UserEmail}", messageId, chatRoomId, userEmail);
            return null;
        }
    }

    public async Task<ChatMessageResponse?> SoftDeleteMessageAsync(long chatRoomId, long messageId, string userEmail, CancellationToken cancellationToken = default)
    {
        try
        {
            return await databaseService.SoftDeleteUserMessageAsync(chatRoomId, messageId, userEmail, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete message {MessageId} in chat room {ChatRoomId} by {UserEmail}", messageId, chatRoomId, userEmail);
            return null;
        }
    }

    public async Task<ChatMessageResponse> AddUserMessageAsync(long chatRoomId, string userEmail, string content, CancellationToken cancellationToken = default) =>
        await databaseService.AddUserMessageAsync(chatRoomId, userEmail, content, cancellationToken);

    public async Task<ChatRoomResponse> CreateChatRoomAsync
        (string userEmail, string title, string? aiModel = null, int? maxParticipants = null, CancellationToken cancellationToken = default) =>
        await databaseService.CreateChatRoomAsync(userEmail, title, aiModel, maxParticipants, cancellationToken);

    public async Task<bool> DeleteChatRoomAsync(long chatRoomId, string userEmail, CancellationToken cancellationToken = default) =>
        await databaseService.DeleteChatRoomAsync(chatRoomId, userEmail, cancellationToken);

    public async Task<int> GetActiveParticipantCountAsync(long chatRoomId, CancellationToken cancellationToken = default) =>
        await databaseService.GetActiveParticipantCountAsync(chatRoomId, cancellationToken);

    public async Task<List<ChatRoomResponse>> GetAllActiveChatRoomsAsync(string userEmail, CancellationToken cancellationToken = default) =>
        await databaseService.GetAllActiveChatRoomsAsync(userEmail, cancellationToken);

    public async Task<List<ChatMessageResponse>> GetChatMessagesAsync
        (long chatRoomId, string userEmail, int limit = 100, long? beforeMessageId = null, CancellationToken cancellationToken = default) =>
        await databaseService.GetChatMessagesAsync(chatRoomId, userEmail, limit, beforeMessageId, cancellationToken);

    public async Task<List<ChatParticipantResponse>> GetChatParticipantsAsync
        (long chatRoomId, string userEmail, bool activeOnly = false, CancellationToken cancellationToken = default) =>
        await databaseService.GetChatParticipantsAsync(chatRoomId, userEmail, activeOnly, cancellationToken);

    public async Task<ChatRoomResponse?> GetChatRoomByIdAsync(long chatRoomId, string userEmail, CancellationToken cancellationToken = default) =>
        await databaseService.GetChatRoomByIdAsync(chatRoomId, userEmail, cancellationToken);

    public async Task<int> GetUnreadMessageCountAsync(long chatRoomId, string userEmail, CancellationToken cancellationToken = default) =>
        await databaseService.GetUnreadMessageCountAsync(chatRoomId, userEmail, cancellationToken);

    public async Task<List<ChatRoom>> GetUserActiveRoomsAsync(string userEmail, string connectionId, CancellationToken cancellationToken = default) =>
        await databaseService.GetUserActiveRoomsAsync(userEmail, connectionId, cancellationToken);

    public async Task<List<ChatRoomResponse>> GetUserChatRoomsAsync(string userEmail, CancellationToken cancellationToken = default) =>
        await databaseService.GetUserChatRoomsAsync(userEmail, cancellationToken);

    public async Task<bool> JoinChatRoomAsync(long chatRoomId, string userEmail, string connectionId, CancellationToken cancellationToken = default) =>
        await databaseService.JoinChatRoomAsync(chatRoomId, userEmail, connectionId, cancellationToken);

    public async Task<bool> LeaveChatRoomAsync(long chatRoomId, string userEmail, CancellationToken cancellationToken = default) =>
        await databaseService.LeaveChatRoomAsync(chatRoomId, userEmail, cancellationToken);

    public async Task MarkUserAsConnectedAsync(long chatRoomId, string userEmail, string connectionId, CancellationToken cancellationToken = default) =>
        await databaseService.MarkUserAsConnectedAsync(chatRoomId, userEmail, connectionId, cancellationToken);

    public async Task MarkUserAsDisconnectedAsync(long chatRoomId, string userEmail, string connectionId, CancellationToken cancellationToken = default) =>
        await databaseService.MarkUserAsDisconnectedAsync(chatRoomId, userEmail, connectionId, cancellationToken);

    public async Task UpdateReadReceiptAsync(long chatRoomId, string userEmail, long lastReadMessageId, CancellationToken cancellationToken = default) =>
        await databaseService.UpdateReadReceiptAsync(chatRoomId, userEmail, lastReadMessageId, cancellationToken);
}
