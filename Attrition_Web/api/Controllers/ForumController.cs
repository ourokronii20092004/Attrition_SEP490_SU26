using Attrition.API.DTOs;
using Attrition.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attrition.API.Controllers;

[ApiController]
[Route("api/forum")]
public class ForumController : ControllerBase
{
    private readonly IForumService _forum;
    public ForumController(IForumService forum) => _forum = forum;

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories() 
        => Ok(new ApiResponse<List<ForumCategoryDto>>(true, await _forum.GetCategoriesAsync()));

    [HttpGet("threads")]
    public async Task<IActionResult> GetThreads([FromQuery] string? category, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(new ApiResponse<PaginatedResponse<ForumThreadListDto>>(true, await _forum.GetThreadsAsync(category, search, page, pageSize)));

    [HttpGet("threads/{id:guid}")]
    public async Task<IActionResult> GetThread(Guid id)
    {
        var result = await _forum.GetThreadAsync(id);
        return result != null ? Ok(new ApiResponse<ForumThreadDto>(true, result)) : NotFound(new ApiResponse(false, "Thread not found"));
    }

    [HttpGet("threads/{id:guid}/posts")]
    public async Task<IActionResult> GetPosts(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        Guid? userId = User.Identity?.IsAuthenticated == true ? Guid.Parse(User.FindFirst("sub")!.Value) : null;
        var result = await _forum.GetPostsAsync(id, page, pageSize, userId);
        return Ok(new ApiResponse<PaginatedResponse<ForumPostDto>>(true, result));
    }

    [Authorize]
    [HttpPost("threads")]
    public async Task<IActionResult> CreateThread(CreateThreadRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _forum.CreateThreadAsync(request, userId);
        return result.Success ? CreatedAtAction(nameof(GetThread), new { id = result.Data }, result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("threads/{id:guid}/posts")]
    public async Task<IActionResult> CreatePost(Guid id, CreatePostRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _forum.CreatePostAsync(id, request, userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPut("posts/{id:guid}")]
    public async Task<IActionResult> UpdatePost(Guid id, UpdatePostRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _forum.UpdatePostAsync(id, request, userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpDelete("posts/{id:guid}")]
    public async Task<IActionResult> DeletePost(Guid id)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "User";
        var result = await _forum.DeletePostAsync(id, userId, role);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("posts/{id:guid}/react")]
    public async Task<IActionResult> React(Guid id, ReactRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _forum.ToggleReactionAsync(id, userId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // Moderation (Admin only)
    [Authorize(Roles = "Admin")]
    [HttpPut("threads/{id:guid}/pin")]
    public async Task<IActionResult> TogglePin(Guid id)
    {
        var result = await _forum.TogglePinAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("threads/{id:guid}/lock")]
    public async Task<IActionResult> ToggleLock(Guid id)
    {
        var result = await _forum.ToggleLockAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("threads/{id:guid}")]
    public async Task<IActionResult> DeleteThread(Guid id)
    {
        var result = await _forum.DeleteThreadAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("posts/{id:guid}/attachments")]
    public async Task<IActionResult> SaveAttachments(Guid id, [FromBody] List<string> urls)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _forum.SavePostAttachmentsAsync(id, urls, userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("threads/{id:guid}/subscribe")]
    public async Task<IActionResult> Subscribe(Guid id)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _forum.ToggleThreadSubscriptionAsync(id, userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("posts/{id:guid}/report")]
    public async Task<IActionResult> ReportPost(Guid id, [FromBody] ReportPostReq req)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _forum.ReportPostAsync(id, req.Reason, userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}