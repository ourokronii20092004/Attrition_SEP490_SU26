using Attrition.API.Models;

namespace Attrition.API.Services;

public interface IRoomService
{
    Task<GameRoom> CreateRoomAsync(Guid hostUserId, string? roomName = null, bool isPrivate = false);
    Task<GameRoom?> GetRoomByCodeAsync(string code);
    Task<bool> JoinRoomAsync(Guid roomId, Guid userId, Guid characterId);
    Task<bool> LeaveRoomAsync(Guid roomId, Guid userId);
    Task<List<GameRoom>> GetPublicRoomsAsync();
    Task<List<GameRoom>> GetAllRoomsAsync();
    Task<bool> ToggleReadyAsync(Guid roomId, Guid userId);
    Task<bool> StartRoomAsync(Guid roomId, Guid hostUserId);
    Task<bool> EndRoomAsync(Guid roomId, Guid hostUserId);
}
