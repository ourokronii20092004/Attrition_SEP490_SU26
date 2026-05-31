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

    private Author AuthorFrom(Guid userId) =>
        new(userId, _user.Username ?? "Unknown", null, _user.IsAdmin ? "Admin" : "User");

    // Soft email-verification gate: unverified users may browse but not contribute.
    private const string VerifyMessage = "Please verify your email address before posting.";
    private IActionResult? RequireVerified() =>
        _user.IsEmailVerified ? null : StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(VerifyMessage));

    // Author/ownership failures should be 403 and missing rows 404 — not a blanket 400.
    private IActionResult MapWriteFailure(ApiResponse result)
    {
        var err = result.Error ?? "";
        if (err.Contains("not found", StringComparison.OrdinalIgnoreCase)) return NotFound(result);
        if (err.Contains("author", StringComparison.OrdinalIgnoreCase)
            || err.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
            || err.Contains("permission", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden, result);
        return BadRequest(result);
    }

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
        // A missing thread should 404, not return an empty 200 (which reads as "thread exists, no posts").
        if (await _forum.GetThreadAsync(id) is null)
            return NotFound(ApiResponse.Fail("Thread not found."));
        var result = await _forum.GetPostsAsync(id, page, pageSize, _user.UserId);
        return Ok(ApiResponse<PaginatedResponse<ForumPostDto>>.Ok(result));
    }

    [Authorize]
    [HttpPost("threads")]
    public async Task<IActionResult> CreateThread(CreateThreadRequest request)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        if (RequireVerified() is { } block) return block;
        var result = await _forum.CreateThreadAsync(request, AuthorFrom(userId));
        return result.Success
            ? CreatedAtAction(nameof(GetThread), new { id = result.Data }, result)
            : BadRequest(result);
    }

    [Authorize]
    [HttpPost("threads/{id:guid}/posts")]
    public async Task<IActionResult> CreatePost(Guid id, CreatePostRequest request)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        if (RequireVerified() is { } block) return block;
        var result = await _forum.CreatePostAsync(id, request, AuthorFrom(userId));
        return result.Success ? Ok(result) : MapWriteFailure(result);
    }

    [Authorize]
    [HttpPut("posts/{id:guid}")]
    public async Task<IActionResult> UpdatePost(Guid id, UpdatePostRequest request)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _forum.UpdatePostAsync(id, request, userId);
        return result.Success ? Ok(result) : MapWriteFailure(result);
    }

    [Authorize]
    [HttpDelete("posts/{id:guid}")]
    public async Task<IActionResult> DeletePost(Guid id)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _forum.DeletePostAsync(id, userId, _user.IsAdmin);
        return result.Success ? Ok(result) : MapWriteFailure(result);
    }

    [Authorize]
    [HttpPost("posts/{id:guid}/react")]
    public async Task<IActionResult> React(Guid id, ReactRequest request)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _forum.ToggleReactionAsync(id, userId, request);
        return result.Success ? Ok(result) : MapWriteFailure(result);
    }

    [Authorize]
    [HttpPost("posts/{id:guid}/attachments")]
    public async Task<IActionResult> SaveAttachments(Guid id, [FromBody] List<string> urls)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        if (urls is null || urls.Count == 0)
            return BadRequest(ApiResponse.Fail("At least one attachment URL is required."));
        if (urls.Count > 20)
            return BadRequest(ApiResponse.Fail("A maximum of 20 attachments is allowed."));
        if (urls.Any(u => !Uri.TryCreate(u, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)))
            return BadRequest(ApiResponse.Fail("All attachments must be valid http(s) URLs."));
        var result = await _forum.SavePostAttachmentsAsync(id, urls, userId);
        return result.Success ? Ok(result) : MapWriteFailure(result);
    }

    [Authorize]
    [HttpPost("threads/{id:guid}/subscribe")]
    public async Task<IActionResult> Subscribe(Guid id)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _forum.ToggleThreadSubscriptionAsync(id, userId);
        return result.Success ? Ok(result) : MapWriteFailure(result);
    }

    [Authorize]
    [HttpPost("posts/{id:guid}/report")]
    public async Task<IActionResult> ReportPost(Guid id, [FromBody] ReportPostReq req)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _forum.ReportPostAsync(id, req.Reason, AuthorFrom(userId));
        return result.Success ? Ok(result) : MapWriteFailure(result);
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

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var result = await _forum.DeleteCategoryAsync(id);
        if (result.Success) return Ok(result);
        // "not found" -> 404; "still has threads" (a conflict) -> 409; else 400.
        var err = result.Error ?? "";
        if (err.Contains("not found", StringComparison.OrdinalIgnoreCase)) return NotFound(result);
        if (err.Contains("threads", StringComparison.OrdinalIgnoreCase))
            return Conflict(result);
        return BadRequest(result);
    }
}
