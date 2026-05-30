using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Forum.Service.DTOs;
using Forum.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Forum.Service.Controllers;

[ApiController]
[Route("api/forum")]
public class ForumController : ControllerBase
{
    private readonly IForumService _forum;
    private readonly ICurrentUser _user;

    public ForumController(IForumService forum, ICurrentUser user)
    {
        _forum = forum;
        _user = user;
    }

    private Author CurrentAuthor => new(_user.UserId!.Value, _user.Username ?? "Unknown", null, _user.IsAdmin ? "Admin" : "User");

    // Soft email-verification gate: unverified users may browse but not contribute.
    private const string VerifyMessage = "Please verify your email address before posting.";
    private IActionResult? RequireVerified() =>
        _user.IsEmailVerified ? null : StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(VerifyMessage));

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
        => Ok(ApiResponse<List<ForumCategoryDto>>.Ok(await _forum.GetCategoriesAsync()));

    [HttpGet("threads")]
    public async Task<IActionResult> GetThreads([FromQuery] string? category, [FromQuery] string? search,
        [FromQuery] Guid? authorId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(ApiResponse<PaginatedResponse<ForumThreadListDto>>.Ok(
            await _forum.GetThreadsAsync(category, search, page, pageSize, authorId)));

    [HttpGet("threads/{id:guid}")]
    public async Task<IActionResult> GetThread(Guid id)
    {
        var result = await _forum.GetThreadAsync(id);
        return result != null
            ? Ok(ApiResponse<ForumThreadDto>.Ok(result))
            : NotFound(ApiResponse.Fail("Thread not found."));
    }

    [HttpGet("threads/{id:guid}/posts")]
    public async Task<IActionResult> GetPosts(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _forum.GetPostsAsync(id, page, pageSize, _user.UserId);
        return Ok(ApiResponse<PaginatedResponse<ForumPostDto>>.Ok(result));
    }

    [Authorize]
    [HttpPost("threads")]
    public async Task<IActionResult> CreateThread(CreateThreadRequest request)
    {
        if (RequireVerified() is { } block) return block;
        var result = await _forum.CreateThreadAsync(request, CurrentAuthor);
        return result.Success
            ? CreatedAtAction(nameof(GetThread), new { id = result.Data }, result)
            : BadRequest(result);
    }

    [Authorize]
    [HttpPost("threads/{id:guid}/posts")]
    public async Task<IActionResult> CreatePost(Guid id, CreatePostRequest request)
    {
        if (RequireVerified() is { } block) return block;
        var result = await _forum.CreatePostAsync(id, request, CurrentAuthor);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPut("posts/{id:guid}")]
    public async Task<IActionResult> UpdatePost(Guid id, UpdatePostRequest request)
    {
        var result = await _forum.UpdatePostAsync(id, request, _user.UserId!.Value);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpDelete("posts/{id:guid}")]
    public async Task<IActionResult> DeletePost(Guid id)
    {
        var result = await _forum.DeletePostAsync(id, _user.UserId!.Value, _user.IsAdmin);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("posts/{id:guid}/react")]
    public async Task<IActionResult> React(Guid id, ReactRequest request)
    {
        var result = await _forum.ToggleReactionAsync(id, _user.UserId!.Value, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("posts/{id:guid}/attachments")]
    public async Task<IActionResult> SaveAttachments(Guid id, [FromBody] List<string> urls)
    {
        var result = await _forum.SavePostAttachmentsAsync(id, urls, _user.UserId!.Value);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("threads/{id:guid}/subscribe")]
    public async Task<IActionResult> Subscribe(Guid id)
    {
        var result = await _forum.ToggleThreadSubscriptionAsync(id, _user.UserId!.Value);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("posts/{id:guid}/report")]
    public async Task<IActionResult> ReportPost(Guid id, [FromBody] ReportPostReq req)
    {
        var result = await _forum.ReportPostAsync(id, req.Reason, CurrentAuthor);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ─── Inline moderation (Admin) ───
    [Authorize(Roles = Roles.Admin)]
    [HttpPut("threads/{id:guid}/pin")]
    public async Task<IActionResult> TogglePin(Guid id)
    {
        var result = await _forum.TogglePinAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("threads/{id:guid}/lock")]
    public async Task<IActionResult> ToggleLock(Guid id)
    {
        var result = await _forum.ToggleLockAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("threads/{id:guid}")]
    public async Task<IActionResult> DeleteThread(Guid id)
    {
        var result = await _forum.DeleteThreadAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ─── Category management (Admin) ───
    [Authorize(Roles = Roles.Admin)]
    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory(ForumCategoryRequest request)
    {
        var result = await _forum.CreateCategoryAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("categories/{id:int}")]
    public async Task<IActionResult> UpdateCategory(int id, ForumCategoryRequest request)
    {
        var result = await _forum.UpdateCategoryAsync(id, request);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
