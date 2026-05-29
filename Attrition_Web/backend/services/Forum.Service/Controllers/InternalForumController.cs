using BuildingBlocks.Contracts;
using Forum.Service.DTOs;
using Forum.Service.Services;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Service.Controllers;

/// <summary>Service-to-service lookups for Search/Admin aggregators. Guarded by X-Internal-Key.</summary>
[ApiController]
[Route("api/internal/forum")]
public class InternalForumController : ControllerBase
{
    private readonly IForumService _forum;
    private readonly IConfiguration _config;

    public InternalForumController(IForumService forum, IConfiguration config)
    {
        _forum = forum;
        _config = config;
    }

    private bool KeyValid()
    {
        var expected = _config["Internal:ApiKey"];
        return !string.IsNullOrEmpty(expected)
            && Request.Headers.TryGetValue("X-Internal-Key", out var got)
            && got == expected;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 5)
    {
        if (!KeyValid()) return Unauthorized();
        if (string.IsNullOrWhiteSpace(q)) return Ok(ApiResponse<List<ForumPostSearchDto>>.Ok(new()));
        return Ok(ApiResponse<List<ForumPostSearchDto>>.Ok(await _forum.SearchAsync(q, limit)));
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        if (!KeyValid()) return Unauthorized();
        var (threads, posts, removed) = await _forum.GetStatsAsync();
        return Ok(ApiResponse<object>.Ok(new { threads, posts, removedPosts = removed }));
    }
}
