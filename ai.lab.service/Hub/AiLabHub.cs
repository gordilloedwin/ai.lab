using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ai.lab.service;

[Authorize]
public class AiLabHub : Hub
{

}