namespace Enemy.Service.Models;

/// <summary>
/// Item config admin sửa trên web → game tải xuống override ItemSO (giống EnemyEntity cho quái).
/// ItemId khớp ItemSO.itemId trong game (network index lookup). Stat riêng phức tạp của từng loại
/// (skill cooldown, projectile...) KHÔNG quản ở đây — chỉ field chung + modifiers cộng chỉ số.
/// </summary>
public class ItemEntity
{
    public string ItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    // Equipment | Accessory | Skill | Material — khớp ItemCategory trong game.
    public string Category { get; set; } = "Material";

    public string Rarity { get; set; } = "Common";
    public string? IconKey { get; set; }
    public string? Description { get; set; }

    // Admin-uploaded artwork URL (synced to the Assets gallery). Distinct from IconKey, which the
    // game client uses for its own sprite lookup.
    public string? ImageUrl { get; set; }

    // Stacking + key-item (BR-41/42/45 trong game).
    public int MaxStack { get; set; } = 1;
    public bool IsKeyItem { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Baseline enables three-way merge without overwriting admin edits.
    public string? UnityBaselineJson { get; set; }
    public DateTime? ImportedAt { get; set; }

    // Cộng chỉ số (StatType + amount) — owned, giống loot của enemy. Game áp khi build item runtime.
    public List<ItemModifierEntry> Modifiers { get; set; } = new();
}

/// <summary>1 dòng cộng chỉ số của item. Stat khớp enum StatType game (MaxHP/AD/AP/DEF/RES/...).</summary>
public class ItemModifierEntry
{
    public string Stat { get; set; } = "AD";
    public int Amount { get; set; }
}
