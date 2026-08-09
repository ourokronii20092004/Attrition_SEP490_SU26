namespace Identity.Service.Models;

/// <summary>
/// Reusable value object for hashed-token + expiry pairs.
/// Mapped as an EF OwnsOne — stored as columns on the parent table, not a separate table.
/// </summary>
public class TokenPair
{
    public string? Token { get; set; }
    public DateTime? ExpiresAt { get; set; }
}