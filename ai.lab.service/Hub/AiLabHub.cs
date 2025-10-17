using ai.lab.service.Model.Outbound;
using ai.lab.service.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ai.lab.service;

[Authorize]
public class AiLabHub : Hub
{
	private readonly IChatService _chatService;
	private readonly ILogger<AiLabHub> _logger;

	public AiLabHub(IChatService chatService, ILogger<AiLabHub> logger)
	{
		_chatService = chatService;
		_logger = logger;
	}

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
				var rooms = await _chatService.GetUserActiveRoomsAsync(email, Context.ConnectionId);
				foreach (var room in rooms)
				{
					await _chatService.MarkUserAsDisconnectedAsync(room.Id, email, Context.ConnectionId);
					await Clients.Group(RoomGroup(room.Id)).SendAsync("ParticipantDisconnected", email);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed handling disconnect for {Email}", email);
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
			var joined = await _chatService.JoinChatRoomAsync(roomId, email, Context.ConnectionId);
			if (!joined)
			{
				return null;
			}

			await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(roomId));

			// Fetch snapshot
			var room = await _chatService.GetChatRoomByIdAsync(roomId, email);
			var participants = await _chatService.GetChatParticipantsAsync(roomId, email);
			var messages = await _chatService.GetChatMessagesAsync(roomId, email, 100);

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
			_logger.LogError(ex, "Failed JoinChatRoom for {Email} room {RoomId}", email, roomId);
			return null;
		}
	}

	public async Task LeaveChatRoom(long roomId)
	{
		var email = GetUserEmail();
		if (string.IsNullOrWhiteSpace(email)) return;

		try
		{
			var left = await _chatService.LeaveChatRoomAsync(roomId, email);
			if (left)
			{
				await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroup(roomId));
				await Clients.Group(RoomGroup(roomId)).SendAsync("ParticipantLeft", email);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed LeaveChatRoom for {Email} room {RoomId}", email, roomId);
		}
	}

	public async Task SendMessage(long roomId, string content)
	{
		var email = GetUserEmail();
		if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(content)) return;
		try
		{
			var message = await _chatService.AddUserMessageAsync(roomId, email, content);
			await Clients.Group(RoomGroup(roomId)).SendAsync("ReceiveMessage", message);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed SendMessage for {Email} room {RoomId}", email, roomId);
		}
	}

	public async Task AskAi(long roomId, string prompt, bool useRag)
	{
		var email = GetUserEmail();
		if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(prompt)) return;
		try
		{
			// Placeholder AI generation (later integrate streaming + RAG logic)
			var aiContent = useRag ? $"[RAG] Response to: {prompt}" : $"AI Response to: {prompt}";
			var message = await _chatService.AddAiMessageAsync(roomId, aiContent);
			await Clients.Group(RoomGroup(roomId)).SendAsync("ReceiveMessage", message);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed AskAi for {Email} room {RoomId}", email, roomId);
		}
	}

	public async Task MarkRead(long roomId, long lastReadMessageId)
	{
		var email = GetUserEmail();
		if (string.IsNullOrWhiteSpace(email)) return;
		try
		{
			await _chatService.UpdateReadReceiptAsync(roomId, email, lastReadMessageId);
			await Clients.Caller.SendAsync("ReadReceiptUpdated", roomId, lastReadMessageId);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed MarkRead for {Email} room {RoomId} msg {MessageId}", email, roomId, lastReadMessageId);
		}
	}
}