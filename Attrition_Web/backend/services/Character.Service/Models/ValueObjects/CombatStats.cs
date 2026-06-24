namespace Character.Service.Models;

/// <summary>
/// Combat-related stats for a character in a session.
/// Mapped as an EF OwnsOne — stored as columns on the parent table, not a separate table.
/// </summary>
public class CombatStats
{
    public float AttackSpeed { get; set; }
    public int PotionMaxFlasks { get; set; }
}
