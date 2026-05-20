namespace Attrition.API.Models;

public class GearEffect
{
    public int GearEffectId { get; set; }
    public int ItemId { get; set; }
    public Gear Gear { get; set; } = null!;
    public string EffectType { get; set; } = string.Empty;
    public float EffectValue { get; set; }
    public float DurationSec { get; set; } = 0;
}