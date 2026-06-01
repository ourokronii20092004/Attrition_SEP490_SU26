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

    /// <summary>Lightweight autocomplete: a flat list of labels + nav targets. Rides the same
    /// 60s search cache. Needs >=2 chars to avoid noisy single-letter fan-outs.</summary>
    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest([FromQuery] string q, [FromQuery] int limit = 5, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Ok(ApiResponse<List<SearchSuggestionDto>>.Ok(new List<SearchSuggestionDto>()));

        var includeUsers = User.Identity?.IsAuthenticated ?? false;
        var safeLimit = Math.Clamp(limit, 1, 10);
        var result = await _search.SuggestAsync(q.Trim(), safeLimit, includeUsers, ct);
        return Ok(ApiResponse<List<SearchSuggestionDto>>.Ok(result));
    }
}
