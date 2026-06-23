namespace Character.Service.Models;

/// <summary>
/// Character's current position and checkpoint within a scene.
/// Mapped as an EF OwnsOne — stored as columns on the parent table, not a separate table.
/// </summary>
public class Position
{
    public float PosX { get; set; }
    public float PosY { get; set; }
    public string? LastRestPointId { get; set; }
}
