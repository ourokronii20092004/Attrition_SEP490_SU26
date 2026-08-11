namespace Character.Service.Models;

/// <summary>
/// Character's current position and checkpoint within a scene.
/// Mapped as an EF OwnsOne — stored as columns on the parent table, not a separate table.
/// </summary>
public class Position
{
    public float PosX { get; set; }
    public float PosY { get; set; }

    // Solo saves keep a Z; co-op stored only X/Y. Carried so both paths round-trip the same shape.
    public float PosZ { get; set; }

    public string? LastRestPointId { get; set; }
}