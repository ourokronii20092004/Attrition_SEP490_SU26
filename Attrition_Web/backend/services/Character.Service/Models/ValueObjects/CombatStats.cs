namespace Character.Service.Models;

/// <summary>
/// Combat-related stats for a character in a session.
/// Mapped as an EF OwnsOne — stored as columns on the parent table, not a separate table.
/// </summary>
public class CombatStats
{
    public float AttackSpeed { get; set; }
    public int PotionMaxFlasks { get; set; }
    public int PotionMaxManaFlasks { get; set; }

    // CURRENT charges left, not just the maxima above. Solo saves have always kept these; co-op
    // did not, so reopening a room silently refilled both flasks.
    public int HealthCharges { get; set; }

    public int ManaCharges { get; set; }

    // Final offence/defence values: base + allocated points + equipped gear, already combined by
    // PlayerStats. They are computed properties in the game (never persisted there), and the web has
    // no gear stat table, so the only way to display them is to store what the client computed.
    public int Ad { get; set; }

    public int Ap { get; set; }
    public int Def { get; set; }
    public int Res { get; set; }
}