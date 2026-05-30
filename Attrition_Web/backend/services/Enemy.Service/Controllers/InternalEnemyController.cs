using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Enemy.Service.DTOs;
using Enemy.Service.Services;
using Microsoft.AspNetCore.Mvc;

namespace Enemy.Service.Controllers;

/// <summary>Service-to-service lookups for Search/Admin aggregators. Guarded by X-Internal-Key.</summary>
[ApiController]
[Route("api/internal/enemies")]
public class InternalEnemyController : ControllerBase
{
    private readonly IEnemyService _service;
    private readonly IConfiguration _config;

    public InternalEnemyController(IEnemyService service, IConfiguration config)
    {
        _service = service;
        _config = config;
    }

    private bool KeyValid() => InternalKey.Validate(Request, _config);

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 5)
    {
        if (!KeyValid()) return Unauthorized();
        if (string.IsNullOrWhiteSpace(q)) return Ok(ApiResponse<List<EnemySummaryDto>>.Ok(new()));
        return Ok(ApiResponse<List<EnemySummaryDto>>.Ok(await _service.SearchAsync(q, limit)));
    }

    [HttpGet("count")]
    public async Task<IActionResult> Count()
    {
        if (!KeyValid()) return Unauthorized();
        return Ok(ApiResponse<int>.Ok(await _service.CountAsync()));
    }
}
