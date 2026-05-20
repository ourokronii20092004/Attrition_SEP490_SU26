namespace Attrition.API.Models;

public class EnemyLootEntry
{
    public string EnemyId { get; set; } = string.Empty;
    public Enemy Enemy { get; set; } = null!;
    
    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public float DropChance { get; set; }
    public short MinQty { get; set; } = 1;
    public short MaxQty { get; set; } = 1;
}