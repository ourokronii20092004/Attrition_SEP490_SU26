using BuildingBlocks.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace BuildingBlocks.Authentication;

/// <summary>
/// Safe current-user extraction for controllers. Replaces the unsafe
/// <c>_user.UserId!.Value</c> and <c>Guid.Parse(User.FindFirst("sub")!.Value)</c> patterns
/// that throw (→ 500) when the token has no valid GUID subject. Returns a 401 envelope instead.
/// </summary>
public static class CurrentUserExtensions
{
    /// <summary>
    /// Pulls the current user id from <see cref="ICurrentUser"/>. Returns null on success
    /// (with <paramref name="userId"/> populated), or an Unauthorized result if the id is missing.
    /// Usage: <c>if (this.RequireUserId(_user, out var userId) is { } error) return error;</c>
    /// </summary>
    public static IActionResult? RequireUserId(
        this ControllerBase controller,
        ICurrentUser currentUser,
        out Guid userId)
    {
        if (currentUser.UserId is { } id)
        {
            userId = id;
            return null;
        }

        userId = default;
        return controller.Unauthorized(ApiResponse.Fail("Valid authentication is required."));
    }
}
