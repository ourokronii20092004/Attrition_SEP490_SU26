namespace Attrition.API.Models;

public class Gear : Item
{
    public string EquipSlot { get; set; } = string.Empty;
    public int BonusAd { get; set; } = 0;
    public int BonusAp { get; set; } = 0;
    public int BonusDef { get; set; } = 0;
    public int BonusRes { get; set; } = 0;
    public float BonusAttackSpeed { get; set; } = 0;
    public int BonusMaxHp { get; set; } = 0;
    public int BonusMaxMana { get; set; } = 0;
    public int RequiredLevel { get; set; } = 1;

    public ICollection<GearEffect> Effects { get; set; } = new List<GearEffect>();
}