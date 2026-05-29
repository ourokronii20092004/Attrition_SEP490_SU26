using Admin.Service.DTOs;
using Admin.Service.Services;
using BuildingBlocks.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Admin.Service.Controllers;

/// <summary>
/// Admin dashboard BFF. Aggregates cross-service stats. Individual admin write
/// operations (ban user, remove post, delete article, enemy CRUD, ...) live on
/// their owning services and are reached directly through the gateway, each
/// Admin-gated and validating the same JWT.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = Roles.Admin)]
public class AdminController : ControllerBase
{
    private readonly IAdminStatsService _stats;
    public AdminController(IAdminStatsService stats) => _stats = stats;

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var stats = await _stats.GetStatsAsync(ct);
        return Ok(ApiResponse<AdminStatsDto>.Ok(stats));
    }
}
