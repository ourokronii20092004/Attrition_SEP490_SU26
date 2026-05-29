using Attrition.API.Models;
using Attrition.API.Repositories;

namespace Attrition.API.Services;

public class RoomService : IRoomService
{
    private readonly IRepository<GameRoom> _roomRepo;
    private readonly IRepository<RoomPlayer> _roomPlayerRepo;

    public RoomService(IRepository<GameRoom> roomRepo, IRepository<RoomPlayer> roomPlayerRepo)
    {
        _roomRepo = roomRepo;
        _roomPlayerRepo = roomPlayerRepo;
    }

    public async Task<GameRoom> CreateRoomAsync(Guid hostUserId, string? roomName = null, bool isPrivate = false)
    {
        string code = string.Empty;
        GameRoom? existingRoom = null;
        int maxRetries = 5;
        int retries = 0;

        do
        {
            code = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
            var (rooms, _) = await _roomRepo.GetPagedAsync(1, 1, r => r.RoomCode == code && r.Status != RoomStatus.Ended);
            existingRoom = rooms.FirstOrDefault();
            retries++;
        } while (existingRoom != null && retries < maxRetries);

        var room = new GameRoom 
        { 
            HostUserId = hostUserId, 
            RoomCode = code,
            RoomName = roomName ?? $"Room {code}",
            IsPrivate = isPrivate
        };
        await _roomRepo.AddAsync(room);
        await _roomPlayerRepo.AddAsync(new RoomPlayer { RoomId = room.RoomId, UserId = hostUserId, PlayerRole = 1, IsReady = true });
        return room;
    }

    public async Task<GameRoom?> GetRoomByCodeAsync(string code)
    {
        var (rooms, _) = await _roomRepo.GetPagedAsync(
            1, 1, 
            r => r.RoomCode == code && r.Status != RoomStatus.Ended,
            null,
            r => r.Players
        );
        return rooms.FirstOrDefault();
    }

    public async Task<bool> JoinRoomAsync(Guid roomId, Guid userId, Guid characterId)
    {
        var (existing, _) = await _roomPlayerRepo.GetPagedAsync(1, 1, rp => rp.RoomId == roomId && rp.UserId == userId);
        if (existing.Count > 0) return true;
        
        var room = await _roomRepo.GetByIdAsync(roomId);
        if (room == null) return false;
        if (room.Status != RoomStatus.Waiting) return false;

        var count = await _roomPlayerRepo.CountAsync(rp => rp.RoomId == roomId);
        if (count >= room.MaxPlayers) return false;

        await _roomPlayerRepo.AddAsync(new RoomPlayer { RoomId = roomId, UserId = userId, CharacterId = characterId });
        return true;
    }

    public async Task<bool> LeaveRoomAsync(Guid roomId, Guid userId)
    {
        var (existing, _) = await _roomPlayerRepo.GetPagedAsync(1, 1, rp => rp.RoomId == roomId && rp.UserId == userId);
        var rp = existing.FirstOrDefault();
        if (rp == null) return false;
        
        var isHost = rp.PlayerRole == 1;
        await _roomPlayerRepo.DeleteAsync(rp);

        if (isHost)
        {
            var (remainingPlayers, _) = await _roomPlayerRepo.GetPagedAsync(
                1, int.MaxValue,
                p => p.RoomId == roomId,
                q => q.OrderBy(p => p.JoinedAt)
            );

            if (remainingPlayers.Count > 0)
            {
                var newHost = remainingPlayers.First();
                newHost.PlayerRole = 1;
                await _roomPlayerRepo.UpdateAsync(newHost);

                var room = await _roomRepo.GetByIdAsync(roomId);
                if (room != null)
                {
                    room.HostUserId = newHost.UserId;
                    await _roomRepo.UpdateAsync(room);
                }
            }
            else
            {
                var room = await _roomRepo.GetByIdAsync(roomId);
                if (room != null)
                {
                    room.Status = RoomStatus.Ended;
                    room.EndedAt = DateTime.UtcNow;
                    await _roomRepo.UpdateAsync(room);
                }
            }
        }

        return true;
    }

    public async Task<List<GameRoom>> GetPublicRoomsAsync()
    {
        var (rooms, _) = await _roomRepo.GetPagedAsync(
            1, int.MaxValue,
            r => !r.IsPrivate && r.Status == RoomStatus.Waiting,
            null,
            r => r.Players
        );
        return rooms;
    }

    public async Task<List<GameRoom>> GetAllRoomsAsync()
    {
        var (rooms, _) = await _roomRepo.GetPagedAsync(
            1, int.MaxValue,
            null,
            q => q.OrderByDescending(r => r.CreatedAt),
            r => r.Players
        );
        return rooms;
    }

    public async Task<bool> ToggleReadyAsync(Guid roomId, Guid userId)
    {
        var (players, _) = await _roomPlayerRepo.GetPagedAsync(1, 1, rp => rp.RoomId == roomId && rp.UserId == userId);
        var rp = players.FirstOrDefault();
        if (rp == null) return false;
        
        rp.IsReady = !rp.IsReady;
        await _roomPlayerRepo.UpdateAsync(rp);
        return true;
    }

    public async Task<bool> StartRoomAsync(Guid roomId, Guid hostUserId)
    {
        var room = await _roomRepo.GetByIdAsync(roomId);
        if (room == null || room.HostUserId != hostUserId || room.Status != RoomStatus.Waiting) return false;
        
        room.Status = RoomStatus.InProgress;
        await _roomRepo.UpdateAsync(room);
        return true;
    }

    public async Task<bool> EndRoomAsync(Guid roomId, Guid hostUserId)
    {
        var room = await _roomRepo.GetByIdAsync(roomId);
        if (room == null || room.HostUserId != hostUserId) return false;
        
        room.Status = RoomStatus.Ended;
        room.EndedAt = DateTime.UtcNow;
        await _roomRepo.UpdateAsync(room);
        return true;
    }
}