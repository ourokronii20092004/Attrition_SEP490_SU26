namespace Attrition.API.Models;

public class CharacterSkill
{
    public Guid CharacterId { get; set; }
    public Character Character { get; set; } = null!;

    public int SkillId { get; set; }
    public Skill Skill { get; set; } = null!;

    public int SkillLevel { get; set; } = 1;
    public bool IsSlotted { get; set; } = false;
    public short? SlotIndex { get; set; }
}