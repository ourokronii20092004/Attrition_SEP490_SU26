namespace Character.Service.Models;

/// <summary>
/// Character vital statistics within a session.
/// Mapped as an EF OwnsOne — stored as columns on the parent table, not a separate table.
/// </summary>
public class VitalStats
{
    public int MaxHp { get; set; }
    public int CurrentHp { get; set; }
    public int MaxMana { get; set; }
    public int CurrentMana { get; set; }
    public int MaxStamina { get; set; }
}
