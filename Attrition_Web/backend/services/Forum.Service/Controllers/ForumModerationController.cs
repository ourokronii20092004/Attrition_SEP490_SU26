using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Forum.Service.DTOs;
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

    private Author Moderator => new(_user.UserId!.Value, _user.Username ?? "Admin", null, "Admin");

    [HttpGet("threads")]
    public async Task<IActionResult> ListThreads()
        => Ok(ApiResponse<List<AdminForumThreadDto>>.Ok(await _forum.ListThreadsForModerationAsync()));

    [HttpGet("posts")]
    public async Task<IActionResult> ListPosts([FromQuery] bool removedOnly = false, [FromQuery] string? search = null)
        => Ok(ApiResponse<List<AdminForumPostDto>>.Ok(await _forum.ListPostsForModerationAsync(removedOnly, search)));

    [HttpPost("posts/{id:guid}/remove")]
    public async Task<IActionResult> RemovePost(Guid id, [FromBody] RemovePostRequest req)
    {
        var result = await _forum.RemovePostAsync(id, Moderator, req.Reason);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("posts/{id:guid}/restore")]
    public async Task<IActionResult> RestorePost(Guid id)
    {
        var result = await _forum.RestorePostAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("reports")]
    public async Task<IActionResult> ListReports([FromQuery] string status = "Pending")
        => Ok(ApiResponse<List<AdminPostReportDto>>.Ok(await _forum.ListReportsAsync(status)));

    [HttpPut("reports/{id:guid}/dismiss")]
    public async Task<IActionResult> DismissReport(Guid id)
    {
        var result = await _forum.DismissReportAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
