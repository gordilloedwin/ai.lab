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

	public async Task AskAi(long roomId, string prompt, bool useRag)
	{
		var email = GetUserEmail();
		if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(prompt))
		{
			return;
		}

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