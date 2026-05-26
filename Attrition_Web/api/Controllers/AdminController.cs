using Attrition.API.DTOs;
using Attrition.API.Services;
using Attrition.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Attrition.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IAdminService _admin;
    private readonly IRoomService _roomService;

    public AdminController(IAuthService auth, IAdminService admin, IRoomService roomService)
    {
        _auth = auth;
        _admin = admin;
        _roomService = roomService;
    }

    // ─── Users ───

    [HttpGet("users")]
    public async Task<IActionResult> ListUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string sort = "joinedAt")
        => Ok(new ApiResponse<PaginatedResponse<AdminUserDto>>(true, await _admin.ListUsersAsync(page, pageSize, search, sort)));

    [HttpPut("users/{id}/role")]
    public async Task<IActionResult> ChangeRole(Guid id, [FromBody] string role)
    {
        var result = await _auth.ChangeRoleAsync(id, role);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("users/{id}/ban")]
    public async Task<IActionResult> ToggleBan(Guid id)
    {
        var result = await _auth.ToggleBanAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("users/{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] string newPassword)
    {
        var result = await _auth.AdminResetPasswordAsync(id, newPassword);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var adminId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _admin.DeleteUserAsync(id, adminId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ─── Wiki Articles ───

    [HttpGet("wiki/articles")]
    public async Task<IActionResult> ListWikiArticles([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
        => Ok(new ApiResponse<PaginatedResponse<AdminWikiArticleDto>>(true, await _admin.ListWikiArticlesAsync(page, pageSize, search)));

    [HttpDelete("wiki/articles/{id}")]
    public async Task<IActionResult> DeleteWikiArticle(Guid id)
    {
        var deleted = await _admin.DeleteWikiArticleAsync(id);
        return deleted ? Ok(new ApiResponse(true)) : NotFound(new ApiResponse(false, "Article not found"));
    }

    // ─── Wiki Categories ───

    [HttpGet("wiki/categories")]
    public async Task<IActionResult> ListWikiCategories([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
        => Ok(new ApiResponse<PaginatedResponse<AdminWikiCategoryDto>>(true, await _admin.ListWikiCategoriesAsync(page, pageSize, search)));

    [HttpPost("wiki/categories")]
    public async Task<IActionResult> CreateWikiCategory([FromBody] WikiCategoryRequest req)
        => Ok(new ApiResponse<WikiCategory>(true, await _admin.CreateWikiCategoryAsync(req)));

    [HttpPut("wiki/categories/{id}")]
    public async Task<IActionResult> UpdateWikiCategory(int id, [FromBody] WikiCategoryRequest req)
    {
        var result = await _admin.UpdateWikiCategoryAsync(id, req);
        return result != null ? Ok(new ApiResponse<WikiCategory>(true, result)) : NotFound(new ApiResponse(false, "Category not found"));
    }

    [HttpDelete("wiki/categories/{id}")]
    public async Task<IActionResult> DeleteWikiCategory(int id)
    {
        var (found, hasArticles) = await _admin.DeleteWikiCategoryAsync(id);
        if (!found) return NotFound(new ApiResponse(false, "Category not found"));
        if (hasArticles) return BadRequest(new ApiResponse(false, "Cannot delete category with articles. Move or delete articles first."));
        return Ok(new ApiResponse(true));
    }

    // ─── Forum Threads ───

    [HttpGet("forum/threads")]
    public async Task<IActionResult> ListForumThreads([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
        => Ok(new ApiResponse<PaginatedResponse<AdminForumThreadDto>>(true, await _admin.ListForumThreadsAsync(page, pageSize, search)));

    [HttpPut("forum/threads/{id}/pin")]
    public async Task<IActionResult> TogglePin(Guid id)
    {
        var result = await _admin.TogglePinAsync(id);
        return result != null ? Ok(new ApiResponse<AdminTogglePinResponse>(true, result)) : NotFound(new ApiResponse(false, "Thread not found"));
    }

    [HttpPut("forum/threads/{id}/lock")]
    public async Task<IActionResult> ToggleLock(Guid id)
    {
        var result = await _admin.ToggleLockAsync(id);
        return result != null ? Ok(new ApiResponse<AdminToggleLockResponse>(true, result)) : NotFound(new ApiResponse(false, "Thread not found"));
    }

    [HttpDelete("forum/threads/{id}")]
    public async Task<IActionResult> DeleteThread(Guid id)
    {
        var deleted = await _admin.DeleteThreadAsync(id);
        return deleted ? Ok(new ApiResponse(true)) : NotFound(new ApiResponse(false, "Thread not found"));
    }

    // ─── Forum Posts (Moderation) ───

    [HttpGet("forum/posts")]
    public async Task<IActionResult> ListForumPosts([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool? removedOnly = null, [FromQuery] string? search = null)
        => Ok(new ApiResponse<PaginatedResponse<AdminForumPostDto>>(true, await _admin.ListForumPostsAsync(page, pageSize, removedOnly, search)));

    [HttpPost("forum/posts/{id}/remove")]
    public async Task<IActionResult> RemovePost(Guid id, [FromBody] RemovePostRequest req)
    {
        var adminId = Guid.Parse(User.FindFirstValue("sub")!);
        var removed = await _admin.RemovePostAsync(id, adminId, req.Reason);
        return removed ? Ok(new ApiResponse(true)) : NotFound(new ApiResponse(false, "Post not found"));
    }

    [HttpPost("forum/posts/{id}/restore")]
    public async Task<IActionResult> RestorePost(Guid id)
    {
        var restored = await _admin.RestorePostAsync(id);
        return restored ? Ok(new ApiResponse(true)) : NotFound(new ApiResponse(false, "Post not found"));
    }

    // ─── Stats ───

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
        => Ok(new ApiResponse<AdminStatsDto>(true, await _admin.GetStatsAsync()));

    [HttpGet("rooms")]
    public async Task<IActionResult> ListRooms()
        => Ok(new ApiResponse<List<GameRoom>>(true, await _roomService.GetAllRoomsAsync()));

    // ─── Forum Reports ───

    [HttpGet("forum/reports")]
    public async Task<IActionResult> ListForumReports([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null)
        => Ok(new ApiResponse<PaginatedResponse<AdminPostReportDto>>(true, await _admin.ListForumReportsAsync(page, pageSize, status)));

    [HttpPut("forum/reports/{id:guid}/dismiss")]
    public async Task<IActionResult> DismissReport(Guid id)
    {
        var result = await _admin.DismissReportAsync(id);
        return result ? Ok(new ApiResponse(true)) : NotFound(new ApiResponse(false, "Report not found"));
    }
}