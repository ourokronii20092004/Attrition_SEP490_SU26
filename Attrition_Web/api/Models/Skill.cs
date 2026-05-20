namespace Attrition.API.Models;

public class Skill
{
    public int SkillId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SkillType { get; set; } = string.Empty;
    public string? DamageType { get; set; }
    public int ManaCost { get; set; } = 0;
    public int StaminaCost { get; set; } = 0;
    public float CooldownSec { get; set; } = 0;
    public int BaseDamage { get; set; } = 0;
    public string? Description { get; set; }
    public string? IconKey { get; set; }
    public int RequiredLevel { get; set; } = 1;

    public ICollection<SkillEffect> Effects { get; set; } = new List<SkillEffect>();
}