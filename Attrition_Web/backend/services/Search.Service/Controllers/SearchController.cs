using BuildingBlocks.Contracts;
using Microsoft.AspNetCore.Mvc;
using Search.Service.DTOs;
using Search.Service.Services;

namespace Search.Service.Controllers;

[ApiController]
[Route("api/search")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _search;
    public SearchController(ISearchService search) => _search = search;

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 5, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(ApiResponse<GlobalSearchResponse>.Ok(
                new GlobalSearchResponse(new List<SearchWikiResultDto>(), new List<SearchUserResultDto>(),
                    new List<SearchPostResultDto>(), new List<SearchEnemyResultDto>(), new List<string>())));

        var includeUsers = User.Identity?.IsAuthenticated ?? false;
        var safeLimit = Math.Clamp(limit, 1, 20);
        var result = await _search.GlobalSearchAsync(q.Trim(), safeLimit, includeUsers, ct);
        return Ok(ApiResponse<GlobalSearchResponse>.Ok(result));
    }
}
