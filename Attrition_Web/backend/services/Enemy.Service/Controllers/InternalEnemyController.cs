using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Enemy.Service.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Enemy.Service.Controllers;

[ApiController]
[Route("api/internal/enemies")]
public class InternalEnemyController : ControllerBase
{
    private readonly IEnemyService _service;
    private readonly IItemService _items;
    private readonly IConfiguration _config;

    public InternalEnemyController(IEnemyService service, IItemService items, IConfiguration config)
    {
        _service = service;
        _items = items;
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

    /// <summary>
    /// Item search for the global search service. Items live in this service alongside enemies, but
    /// are a separate result kind, so they get their own endpoint rather than being folded into the
    /// enemy results.
    /// </summary>
    [HttpGet("items/search")]
    public async Task<IActionResult> SearchItems([FromQuery] string q, [FromQuery] int limit = 5)
    {
        if (!KeyValid()) return Unauthorized(ApiResponse.Fail("Valid service authentication is required."));
        if (string.IsNullOrWhiteSpace(q)) return Ok(ApiResponse<List<ItemResponse>>.Ok(new()));

        // GetAllWithModifiersAsync already matches name OR id and is cached; take the head of that
        // ordered list rather than adding a second, subtly different query.
        var matches = await _items.GetAllAsync(category: null, search: q);
        return Ok(ApiResponse<List<ItemResponse>>.Ok(matches.Take(Math.Clamp(limit, 1, 50)).ToList()));
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
        var (enemies, items) = await _service.GetStatsAsync();
        return Ok(ApiResponse<object>.Ok(new { enemies, items }));
    }
}