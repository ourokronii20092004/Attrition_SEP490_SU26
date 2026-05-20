namespace Attrition.API.Services;

using StackExchange.Redis;

public class GameSessionService
{
    private readonly IConnectionMultiplexer _redis;
    public GameSessionService(IConnectionMultiplexer redis) => _redis = redis;
}