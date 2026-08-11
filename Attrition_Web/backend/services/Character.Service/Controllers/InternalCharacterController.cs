using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Character.Service.Controllers;

/// <summary>
/// Service-to-service counters for the admin dashboard. Not routed through the gateway — the admin
/// service calls it directly with the shared internal key, like every other /api/internal/* surface.
/// </summary>
[ApiController]
[Route("api/internal/characters")]
public class InternalCharacterController(
    ICharacterService characters,
    ISessionService sessions,
    IConfiguration config) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        if (!InternalKey.Validate(Request, config))
            return Unauthorized(ApiResponse.Fail("Valid service authentication is required."));

        var characterCount = await characters.CountAsync();
        var (rooms, multiplayer) = await sessions.GetRoomStatsAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { characters = characterCount, rooms, coopRooms = multiplayer }));
    }
}