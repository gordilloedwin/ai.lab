using ai.lab.service.Model.Outbound;
using ai.lab.service.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ai.lab.service;

[Authorize]
public class AiLabHub(ILogger<AiLabHub> logger, IChatService chatService, IAIService aIService) : Hub
{
    private string? GetUserEmail() => Context.User?.FindFirst(ClaimTypes.Email)?.Value
		?? Context.User?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value
		?? Context.User?.FindFirst("email")?.Value;

	public override async Task OnDisconnectedAsync(Exception? exception)
	{
		var email = GetUserEmail();
		if (!string.IsNullOrWhiteSpace(email))
		{
			try
			{
				// Get rooms tied to this connection and mark user disconnected
				var rooms = await chatService.GetUserActiveRoomsAsync(email, Context.ConnectionId);
				foreach (var room in rooms)
				{
					await chatService.MarkUserAsDisconnectedAsync(room.Id, email, Context.ConnectionId);
					await Clients.Group(RoomGroup(room.Id)).SendAsync("ParticipantDisconnected", email);
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed handling disconnect for {Email}", email);
			}
		}
		await base.OnDisconnectedAsync(exception);
	}

	private static string RoomGroup(long roomId) => $"chat-room-{roomId}";

	public async Task<ChatRoomInitResponse?> JoinChatRoom(long roomId)
	{
		var email = GetUserEmail();
		if (string.IsNullOrWhiteSpace(email)) return null;

		try
		{
			var joined = await chatService.JoinChatRoomAsync(roomId, email, Context.ConnectionId);
			if (!joined)
			{
				return null;
			}

			await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(roomId));

			// Fetch snapshot
			var room = await chatService.GetChatRoomByIdAsync(roomId, email);
			var participants = await chatService.GetChatParticipantsAsync(roomId, email);
			var messages = await chatService.GetChatMessagesAsync(roomId, email, 100);

			// Broadcast participant joined
			await Clients.Group(RoomGroup(roomId)).SendAsync("ParticipantJoined", email);

			return new ChatRoomInitResponse
			{
				Room = room,
				Participants = participants,
				Messages = messages
			};
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed JoinChatRoom for {Email} room {RoomId}", email, roomId);
			return null;
		}
	}

	public async Task LeaveChatRoom(long roomId)
	{
		var email = GetUserEmail();
		if (string.IsNullOrWhiteSpace(email)) return;

		try
		{
			var left = await chatService.LeaveChatRoomAsync(roomId, email);
			if (left)
			{
				await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroup(roomId));
				await Clients.Group(RoomGroup(roomId)).SendAsync("ParticipantLeft", email);
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed LeaveChatRoom for {Email} room {RoomId}", email, roomId);
		}
	}

	public async Task SendMessage(long roomId, string content)
	{
		var email = GetUserEmail();
		if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(content))
		{
			return;
		}

		try
		{
			var message = await chatService.AddUserMessageAsync(roomId, email, content);
			await Clients.Group(RoomGroup(roomId)).SendAsync("ReceiveMessage", message);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed SendMessage for {Email} room {RoomId}", email, roomId);
		}
	}

	// Static in-memory lock map; consider distributed cache if scaling out.
	private static readonly Dictionary<long, string> _roomAiLocks = new(); // roomId -> lockingConnectionId
	private static readonly object _lockSync = new();

	private bool TryAcquireAiLock(long roomId)
	{
		lock (_lockSync)
		{
			if (_roomAiLocks.ContainsKey(roomId)) return false;
			_roomAiLocks[roomId] = Context.ConnectionId;
			return true;
		}
	}

	private bool ReleaseAiLock(long roomId)
	{
		lock (_lockSync)
		{
			if (_roomAiLocks.TryGetValue(roomId, out var holder) && holder == Context.ConnectionId)
			{
				_roomAiLocks.Remove(roomId);
				return true;
			}
			return false;
		}
	}
	private static bool IsAiLocked(long roomId)
	{
		lock (_lockSync)
		{
			return _roomAiLocks.ContainsKey(roomId);
		}
	}

	public async Task AskAi(long roomId, string prompt, bool useRag)
	{
		var email = GetUserEmail();
		if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(prompt))
		{
			return;
		}

		// Attempt lock
		if (!TryAcquireAiLock(roomId))
		{
			await Clients.Caller.SendAsync("AiBusyRejected", roomId, "AI is currently answering another question. Please wait.");
			return;
		}
		await Clients.Group(RoomGroup(roomId)).SendAsync("AiBusyStateChanged", roomId, true);

		try
		{
			var userQuestionMessage = await chatService.AddUserMessageAsync(roomId, email, prompt);
			await Clients.Group(RoomGroup(roomId)).SendAsync("ReceiveMessage", userQuestionMessage);

			var room = await chatService.GetChatRoomByIdAsync(roomId, email);
			var modelToUse = room?.AiModel ?? "llama3:latest";
			var aiResponse = await aIService.GenerateResponseFromApiAsync(modelToUse!, prompt, email);
			var aiMessage = await chatService.AddAiMessageAsync(roomId, aiResponse.Response ?? "[[AI-Unavailable]]");
			await Clients.Group(RoomGroup(roomId)).SendAsync("ReceiveMessage", aiMessage);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed AskAi for {Email} room {RoomId}", email, roomId);
		}
		finally
		{
			ReleaseAiLock(roomId);
			await Clients.Group(RoomGroup(roomId)).SendAsync("AiBusyStateChanged", roomId, false);
		}
	}

	/// <summary>
	/// Streams an AI response token-by-token to the room providing a typing indicator UX.
	/// Emits events: AiTypingStarted(roomId, streamId), AiTypingChunk(roomId, streamId, chunk), AiTypingCompleted(roomId, streamId, finalMessage), AiTypingError(roomId, streamId, errorMessage)
	/// </summary>
	public async Task AskAiStream(long roomId, string prompt, bool useRag)
	{
		var email = GetUserEmail();
		if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(prompt)) return;

		// Attempt lock
		if (!TryAcquireAiLock(roomId))
		{
			await Clients.Caller.SendAsync("AiBusyRejected", roomId, "AI is currently answering another question. Please wait.");
			return;
		}
		await Clients.Group(RoomGroup(roomId)).SendAsync("AiBusyStateChanged", roomId, true);

		var streamId = Guid.NewGuid().ToString("N");
		try
		{
			// Echo user's prompt first (same as AskAi)
			var userQuestionMessage = await chatService.AddUserMessageAsync(roomId, email, prompt);
			await Clients.Group(RoomGroup(roomId)).SendAsync("ReceiveMessage", userQuestionMessage);

			await Clients.Group(RoomGroup(roomId)).SendAsync("AiTypingStarted", roomId, streamId);

			var room = await chatService.GetChatRoomByIdAsync(roomId, email);
			var modelToUse = room?.AiModel ?? "llama3:latest";

			// Choose API or RAG prompt path before streaming
			string finalPrompt = prompt;
			if (useRag)
			{
				// For streaming, we currently reuse non-stream RAG builder by asking AI service to build context first
				// Simpler approach: prepend instruction; in future call embedding manager directly.
				finalPrompt = $"Use any provided context to answer. Question: {prompt}"; // Minimal placeholder
			}

			var accumulated = new System.Text.StringBuilder();
			await foreach (var chunk in aIService.StreamResponseAsync(email, modelToUse!, finalPrompt))
			{
				if (chunk == "\n\n[[DONE]]") break;
				accumulated.Append(chunk);
				await Clients.Group(RoomGroup(roomId)).SendAsync("AiTypingChunk", roomId, streamId, chunk);
			}

			var aiMessage = await chatService.AddAiMessageAsync(roomId, accumulated.ToString());
			await Clients.Group(RoomGroup(roomId)).SendAsync("AiTypingCompleted", roomId, streamId, aiMessage);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed AskAiStream for {Email} room {RoomId}", email, roomId);
			await Clients.Group(RoomGroup(roomId)).SendAsync("AiTypingError", roomId, streamId, "AI stream failed.");
		}
		finally
		{
			ReleaseAiLock(roomId);
			await Clients.Group(RoomGroup(roomId)).SendAsync("AiBusyStateChanged", roomId, false);
		}
	}

	/// <summary>
	/// Updates a user message content and broadcasts update.
	/// </summary>
	public async Task EditMessage(long roomId, long messageId, string newContent)
	{
		var email = GetUserEmail();
		if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(newContent)) return;
		try
		{
			var updated = await chatService.UpdateMessageContentAsync(roomId, messageId, email, newContent);
			if (updated != null)
			{
				await Clients.Group(RoomGroup(roomId)).SendAsync("MessageUpdated", updated);
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed EditMessage for {Email} room {RoomId} msg {MessageId}", email, roomId, messageId);
		}
	}

	/// <summary>
	/// Soft deletes a user message (marks content as [[deleted]]) and broadcasts deletion.
	/// </summary>
	public async Task DeleteMessage(long roomId, long messageId)
	{
		var email = GetUserEmail();
		if (string.IsNullOrWhiteSpace(email)) return;
		try
		{
			var deleted = await chatService.SoftDeleteMessageAsync(roomId, messageId, email);
			if (deleted != null)
			{
				await Clients.Group(RoomGroup(roomId)).SendAsync("MessageDeleted", deleted);
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed DeleteMessage for {Email} room {RoomId} msg {MessageId}", email, roomId, messageId);
		}
	}

	public async Task MarkRead(long roomId, long lastReadMessageId)
	{
		var email = GetUserEmail();
		if (string.IsNullOrWhiteSpace(email)) return;
		try
		{
			await chatService.UpdateReadReceiptAsync(roomId, email, lastReadMessageId);
			// Broadcast to group so others can update read-by indicators
			await Clients.Group(RoomGroup(roomId)).SendAsync("ReadReceiptUpdated", roomId, email, lastReadMessageId);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed MarkRead for {Email} room {RoomId} msg {MessageId}", email, roomId, lastReadMessageId);
		}
	}
}