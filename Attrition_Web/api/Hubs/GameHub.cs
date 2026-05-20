namespace Attrition.API.Hubs;

using Microsoft.AspNetCore.SignalR;
using Attrition.API.Services;

public class GameHub : Hub
{
    public async Task JoinRoom(string roomCode)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
        await Clients.Group(roomCode).SendAsync("OnPlayerJoined", Context.ConnectionId);
    }

    public async Task LeaveRoom(string roomCode)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomCode);
        await Clients.Group(roomCode).SendAsync("OnPlayerLeft", Context.ConnectionId);
    }

    public async Task SendInput(string roomCode, object inputPayload)
    {
        await Clients.OthersInGroup(roomCode).SendAsync("OnPlayerAction", Context.ConnectionId, inputPayload);
    }
}