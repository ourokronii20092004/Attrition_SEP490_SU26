using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Microsoft.AspNetCore.Mvc;
using Wiki.Service.DTOs;
using Wiki.Service.Services;

namespace Wiki.Service.Controllers;

/// <summary>Service-to-service lookups for Search/Admin aggregators. Guarded by X-Internal-Key.</summary>
[ApiController]
[Route("api/internal/wiki")]
public class InternalWikiController : ControllerBase
{
    private readonly IWikiService _wiki;
    private readonly IConfiguration _config;

    public InternalWikiController(IWikiService wiki, IConfiguration config)
    {
        _wiki = wiki;
        _config = config;
    }

    private bool KeyValid() => InternalKey.Validate(Request, _config);

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 5)
    {
        if (!KeyValid()) return Unauthorized(ApiResponse.Fail("Valid service authentication is required."));
        if (string.IsNullOrWhiteSpace(q)) return Ok(ApiResponse<List<WikiSearchResultDto>>.Ok(new()));
        return Ok(ApiResponse<List<WikiSearchResultDto>>.Ok(await _wiki.SearchAsync(q, Math.Clamp(limit, 1, 50))));
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        if (!KeyValid()) return Unauthorized(ApiResponse.Fail("Valid service authentication is required."));
        var articles = await _wiki.CountArticlesAsync();
        var pending = await _wiki.CountPendingContributionsAsync();
        return Ok(ApiResponse<object>.Ok(new { articles, pendingContributions = pending }));
    }
}
