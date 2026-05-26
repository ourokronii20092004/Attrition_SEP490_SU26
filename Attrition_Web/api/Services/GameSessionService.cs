using StackExchange.Redis;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Attrition.API.Services;

public class GameSessionService : IGameSessionService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;

    public GameSessionService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _db = redis.GetDatabase();
    }

    public async Task<string?> GetRoomStateAsync(string roomCode)
    {
        return await _db.StringGetAsync($"room:{roomCode}:state");
    }

    public async Task SaveRoomStateAsync(string roomCode, string stateJson)
    {
        await _db.StringSetAsync($"room:{roomCode}:state", stateJson, TimeSpan.FromHours(2));
    }

    public async Task PublishCommandAsync(string roomCode, string command, string payload)
    {
        var pubsub = _redis.GetSubscriber();
        var message = JsonSerializer.Serialize(new { Command = command, Payload = payload, Timestamp = DateTime.UtcNow });
        await pubsub.PublishAsync(RedisChannel.Literal($"room:{roomCode}:commands"), message);
    }
}