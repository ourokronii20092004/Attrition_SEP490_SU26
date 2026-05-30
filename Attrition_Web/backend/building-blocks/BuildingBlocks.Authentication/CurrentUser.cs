using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using BuildingBlocks.Contracts;

namespace BuildingBlocks.Authentication;

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Username { get; }
    bool IsAdmin { get; }
    bool IsAuthenticated { get; }
    bool IsEmailVerified { get; }
}

public sealed class CurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal? _principal;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _principal = accessor.HttpContext?.User;
    }

    public Guid? UserId =>
        Guid.TryParse(_principal?.FindFirstValue("sub"), out var id) ? id : null;

    public string? Username => _principal?.FindFirstValue("username");

    public bool IsAdmin => _principal?.IsInRole(Roles.Admin) ?? false;

    public bool IsAuthenticated => _principal?.Identity?.IsAuthenticated ?? false;

    // Email-verification gate. Admins are always treated as verified so seeded/promoted
    // admin accounts are never blocked from acting.
    public bool IsEmailVerified =>
        IsAdmin || string.Equals(_principal?.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);
}
