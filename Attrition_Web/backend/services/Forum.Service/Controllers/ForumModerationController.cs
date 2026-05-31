using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Forum.Service.DTOs;
using Forum.Service.Models;
using Forum.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Service.Controllers;

[ApiController]
[Route("api/admin/forum")]
[Authorize(Roles = Roles.Admin)]
public class ForumModerationController : ControllerBase
{
    private readonly IForumService _forum;
    private readonly ICurrentUser _user;

    public ForumModerationController(IForumService forum, ICurrentUser user)
    {
        _forum = forum;
        _user = user;
    }

    private Author ModeratorFrom(Guid userId) => new(userId, _user.Username ?? "Admin", null, "Admin");

    [HttpGet("threads")]
    public async Task<IActionResult> ListThreads([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(ApiResponse<PaginatedResponse<AdminForumThreadDto>>.Ok(
            await _forum.ListThreadsForModerationAsync(page < 1 ? 1 : page, pageSize)));

    [HttpGet("posts")]
    public async Task<IActionResult> ListPosts([FromQuery] bool removedOnly = false, [FromQuery] string? search = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(ApiResponse<PaginatedResponse<AdminForumPostDto>>.Ok(
            await _forum.ListPostsForModerationAsync(removedOnly, search, page < 1 ? 1 : page, pageSize)));

    [HttpPost("posts/{id:guid}/remove")]
    public async Task<IActionResult> RemovePost(Guid id, [FromBody] RemovePostRequest req)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _forum.RemovePostAsync(id, ModeratorFrom(userId), req.Reason);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("posts/{id:guid}/restore")]
    public async Task<IActionResult> RestorePost(Guid id)
    {
        var result = await _forum.RestorePostAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("reports")]
    public async Task<IActionResult> ListReports([FromQuery] string status = ReportStatus.Pending,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(ApiResponse<PaginatedResponse<AdminPostReportDto>>.Ok(
            await _forum.ListReportsAsync(status, page < 1 ? 1 : page, pageSize)));

    [HttpPut("reports/{id:guid}/dismiss")]
    public async Task<IActionResult> DismissReport(Guid id)
    {
        var result = await _forum.DismissReportAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPut("reports/{id:guid}/resolve")]
    public async Task<IActionResult> ResolveReport(Guid id)
    {
        var result = await _forum.ResolveReportAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
