namespace Attrition.API.Models;

public class Character
{
    public Guid CharacterId { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string CharacterName { get; set; } = string.Empty;
    public string CharacterClass { get; set; } = "wanderer";
    public int CurrentLevel { get; set; } = 1;
    public int CurrentExp { get; set; } = 0;
    public int TotalPlaytime { get; set; } = 0;
    public int Gold { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<GameSave> GameSaves { get; set; } = new List<GameSave>();
    public ICollection<CharacterInventorySlot> Inventory { get; set; } = new List<CharacterInventorySlot>();
    public ICollection<CharacterSkill> Skills { get; set; } = new List<CharacterSkill>();
}