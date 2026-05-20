namespace Attrition.API.Models;

public class SkillEffect
{
    public int SkillEffectId { get; set; }
    public int SkillId { get; set; }
    public Skill Skill { get; set; } = null!;
    public string EffectType { get; set; } = string.Empty;
    public float EffectValue { get; set; }
    public float DurationSec { get; set; } = 0;
}