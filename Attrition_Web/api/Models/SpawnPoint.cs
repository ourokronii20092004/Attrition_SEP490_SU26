using System;

namespace Attrition.API.Models;

public class SpawnPoint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EnemyId { get; set; } = string.Empty;
    public string SceneName { get; set; } = string.Empty;
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public int SpawnIntervalSeconds { get; set; } = 30;
    public int MaxActiveCount { get; set; } = 1;
}
