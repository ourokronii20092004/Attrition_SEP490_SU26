namespace Identity.Service.Models;

/// <summary>
/// Login security tracking — last login info and brute-force lockout state.
/// Mapped as an EF OwnsOne — stored as columns on the parent table, not a separate table.
/// </summary>
public class LoginSecurity
{
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutEnd { get; set; }
}
