namespace Attrition.API.Models;

public class Consumable : Item
{
    public int HpRestore { get; set; } = 0;
    public int ManaRestore { get; set; } = 0;
    public string? BuffType { get; set; }
    public float BuffValue { get; set; } = 0;
    public float BuffDuration { get; set; } = 0;
}