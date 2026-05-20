namespace Attrition.API.Services;

using Attrition.API.Data;
using Attrition.API.Models;
using Microsoft.EntityFrameworkCore;

public class RoomService
{
    private readonly AppDbContext _db;
    public RoomService(AppDbContext db) => _db = db;

    public async Task<GameRoom> CreateRoomAsync(Guid hostUserId)
    {
        var code = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
        var room = new GameRoom { HostUserId = hostUserId, RoomCode = code };
        _db.GameRooms.Add(room);
        _db.RoomPlayers.Add(new RoomPlayer { RoomId = room.RoomId, UserId = hostUserId, PlayerRole = 1 });
        await _db.SaveChangesAsync();
        return room;
    }

    public async Task<GameRoom?> GetRoomByCodeAsync(string code)
        => await _db.GameRooms.Include(r => r.Players).FirstOrDefaultAsync(r => r.RoomCode == code && r.Status != "ended");

    public async Task<bool> JoinRoomAsync(Guid roomId, Guid userId, Guid characterId)
    {
        if (await _db.RoomPlayers.AnyAsync(rp => rp.RoomId == roomId && rp.UserId == userId)) return true;
        
        var room = await _db.GameRooms.Include(r => r.Players).FirstOrDefaultAsync(r => r.RoomId == roomId);
        if (room == null || room.Players.Count >= room.MaxPlayers) return false;

        _db.RoomPlayers.Add(new RoomPlayer { RoomId = roomId, UserId = userId, CharacterId = characterId });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> LeaveRoomAsync(Guid roomId, Guid userId)
    {
        var rp = await _db.RoomPlayers.FirstOrDefaultAsync(p => p.RoomId == roomId && p.UserId == userId);
        if (rp == null) return false;
        
        _db.RoomPlayers.Remove(rp);
        await _db.SaveChangesAsync();
        return true;
    }
}