namespace Attrition.API.Models;

public class GameSave
{
    public Guid SaveId { get; set; } = Guid.NewGuid();
    public Guid CharacterId { get; set; }
    public Character Character { get; set; } = null!;
    public string? SaveName { get; set; }
    public string CurrentScene { get; set; } = string.Empty;
    public string? RestPointId { get; set; }
    public float PositionX { get; set; } = 0;
    public float PositionY { get; set; } = 0;
    public int CurrentHp { get; set; }
    public int MaxHp { get; set; }
    public int CurrentMana { get; set; }
    public int MaxMana { get; set; }
    public int CurrentStamina { get; set; }
    public int MaxStamina { get; set; }
    public bool IsAutoSave { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}