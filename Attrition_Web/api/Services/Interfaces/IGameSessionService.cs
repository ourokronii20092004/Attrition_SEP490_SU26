namespace Attrition.API.Services;

public interface IGameSessionService
{
    Task<string?> GetRoomStateAsync(string roomCode);
    Task SaveRoomStateAsync(string roomCode, string stateJson);
    Task PublishCommandAsync(string roomCode, string command, string payload);
}
