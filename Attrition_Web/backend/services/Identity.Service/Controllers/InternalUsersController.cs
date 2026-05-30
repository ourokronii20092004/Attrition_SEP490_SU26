using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Identity.Service.DTOs;
using Identity.Service.Services;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Service.Controllers;

/// <summary>
/// Service-to-service lookup endpoints for Search/Admin aggregators.
/// Guarded by a shared internal API key (X-Internal-Key), not user JWT,
/// because callers act on behalf of the platform, not an end user.
/// </summary>
[ApiController]
[Route("api/internal/users")]
public class InternalUsersController : ControllerBase
{
    private readonly IAdminUserService _admin;
    private readonly IConfiguration _config;

    public InternalUsersController(IAdminUserService admin, IConfiguration config)
    {
        _admin = admin;
        _config = config;
    }

    private bool KeyValid() => InternalKey.Validate(Request, _config);

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 5)
    {
        if (!KeyValid()) return Unauthorized();
        if (string.IsNullOrWhiteSpace(q)) return Ok(ApiResponse<List<UserSummaryDto>>.Ok(new()));
        var users = await _admin.SearchAsync(q, Math.Clamp(limit, 1, 50));
        return Ok(ApiResponse<List<UserSummaryDto>>.Ok(users));
    }

    [HttpPost("batch")]
    public async Task<IActionResult> Batch([FromBody] List<Guid> ids)
    {
        if (!KeyValid()) return Unauthorized();
        if (ids.Count > 200)
            return BadRequest(ApiResponse.Fail("Too many ids requested (max 200)."));
        var users = await _admin.GetByIdsAsync(ids);
        return Ok(ApiResponse<List<UserSummaryDto>>.Ok(users));
    }

    [HttpGet("count")]
    public async Task<IActionResult> Count()
    {
        if (!KeyValid()) return Unauthorized();
        var count = await _admin.CountAsync();
        return Ok(ApiResponse<int>.Ok(count));
    }
}
