namespace Attrition.API.Models;

public class Item
{
    public int ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string Rarity { get; set; } = "common";
    public short StackLimit { get; set; } = 1;
    public string? Description { get; set; }
    public string? IconKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}