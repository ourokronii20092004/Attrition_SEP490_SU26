namespace Attrition.API.Hubs;

using Microsoft.AspNetCore.SignalR;
using Attrition.API.Services;
using Attrition.API.Repositories;
using Attrition.API.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System;
using System.Linq;
using System.Threading.Tasks;

[Authorize]
public class GameHub : Hub
{
    private readonly IRoomService _roomService;
    private readonly IRepository<RoomPlayer> _roomPlayerRepo;

    public GameHub(IRoomService roomService, IRepository<RoomPlayer> roomPlayerRepo)
    {
        _roomService = roomService;
        _roomPlayerRepo = roomPlayerRepo;
    }

    public async Task JoinRoom(string roomCode)
    {
        var userIdStr = Context.User?.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            throw new HubException("Unauthorized");
        }

        var room = await _roomService.GetRoomByCodeAsync(roomCode);
        if (room == null)
        {
            throw new HubException("Room not found");
        }

        var isMember = room.Players.Any(p => p.UserId == userId);
        if (!isMember)
        {
            throw new HubException("You are not a member of this room");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
        await Clients.Group(roomCode).SendAsync("OnPlayerJoined", userId.ToString());
    }

    public async Task LeaveRoom(string roomCode)
    {
        var userIdStr = Context.User?.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            throw new HubException("Unauthorized");
        }

        var room = await _roomService.GetRoomByCodeAsync(roomCode);
        if (room != null)
        {
            await _roomService.LeaveRoomAsync(room.RoomId, userId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomCode);
            await Clients.Group(roomCode).SendAsync("OnPlayerLeft", userId.ToString());
        }
    }

    public async Task SendInput(string roomCode, object inputPayload)
    {
        await Clients.OthersInGroup(roomCode).SendAsync("OnPlayerAction", Context.ConnectionId, inputPayload);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userIdStr = Context.User?.FindFirst("sub")?.Value;
        if (Guid.TryParse(userIdStr, out var userId))
        {
            var (memberships, _) = await _roomPlayerRepo.GetPagedAsync(
                1, int.MaxValue,
                rp => rp.UserId == userId && rp.GameRoom.Status != "ended",
                null,
                rp => rp.GameRoom
            );

            foreach (var membership in memberships)
            {
                var room = membership.GameRoom;
                if (room != null && room.Status != "ended")
                {
                    await _roomService.LeaveRoomAsync(room.RoomId, userId);
                    await Clients.Group(room.RoomCode).SendAsync("OnPlayerLeft", userId.ToString());
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}