using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Enemy.Service.Data;
using Enemy.Service.DTOs;
using Enemy.Service.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Enemy.Service.Controllers;

[ApiController]
[Route("api/internal/enemies")]
public class InternalEnemyController : ControllerBase
{
    private readonly IEnemyService _service;
    private readonly EnemyDbContext _db;
    private readonly IConfiguration _config;

    public InternalEnemyController(IEnemyService service, EnemyDbContext db, IConfiguration config)
    {
        _service = service;
        _db = db;
        _config = config;
    }

    private bool KeyValid() => InternalKey.Validate(Request, _config);

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 5)
    {
        if (!KeyValid()) return Unauthorized(ApiResponse.Fail("Valid service authentication is required."));
        if (string.IsNullOrWhiteSpace(q)) return Ok(ApiResponse<List<EnemySummaryDto>>.Ok(new()));
        return Ok(ApiResponse<List<EnemySummaryDto>>.Ok(await _service.SearchAsync(q, Math.Clamp(limit, 1, 50))));
    }

    [HttpGet("count")]
    public async Task<IActionResult> Count()
    {
        if (!KeyValid()) return Unauthorized(ApiResponse.Fail("Valid service authentication is required."));
        return Ok(ApiResponse<int>.Ok(await _service.CountAsync()));
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        if (!KeyValid()) return Unauthorized(ApiResponse.Fail("Valid service authentication is required."));
        var enemies = await _db.Enemies.CountAsync();
        var items = await _db.Items.CountAsync(x => x.Category != "Skill");
        var skills = await _db.Skills.CountAsync();
        return Ok(ApiResponse<object>.Ok(new { enemies, items, skills }));
    }
}
