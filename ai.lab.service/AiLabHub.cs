using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ai.lab.service;

[Authorize]
public class AiLabHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var email = Context.User?.FindFirst(ClaimTypes.Email)?.Value;
        var name = Context.User?.FindFirst("name")?.Value;
        var avatar = Context.User?.FindFirst("avatar_uri")?.Value;

        // You can broadcast presence or store in-memory
        await Clients.All.SendAsync("UserJoined", new { email, name, avatar });
    }

    public async Task SendMessage(string message)
    {
        var email = Context.User?.FindFirst(ClaimTypes.Email)?.Value;
        var name = Context.User?.FindFirst("name")?.Value;

        await Clients.All.SendAsync("ReceiveMessage", new
        {
            from = name ?? email,
            text = message
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var email = Context.User?.FindFirst(ClaimTypes.Email)?.Value;
        var name = Context.User?.FindFirst("name")?.Value;

        await Clients.All.SendAsync("UserLeft", new
        {
            email,
            name
        });

        await base.OnDisconnectedAsync(exception);
    }

    // Optional: Called by client to send a message to a specific group
    public async Task SendToGroup(string groupName, string user, string message)
    {
        await Clients.Group(groupName).SendAsync("ReceiveMessage", user, message);
    }

    // Optional: Join a group
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    // Optional: Leave a group
    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}