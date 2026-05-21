using Attrition.API.DTOs;
using Attrition.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Attrition.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly AdminService _admin;

    public AdminController(AuthService auth, AdminService admin)
    {
        _auth = auth;
        _admin = admin;
    }

    // ─── Users ───

    [HttpGet("users")]
    public async Task<IActionResult> ListUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string sort = "joinedAt")
        => Ok(await _admin.ListUsersAsync(page, pageSize, search, sort));

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
        if (result == null) return NotFound(new { success = false, error = "User not found." });

        // Check if it's the self-delete error
        var obj = result as dynamic;
        if (obj?.success == false) return BadRequest(result);

        return Ok(result);
    }

    // ─── Wiki Articles ───

    [HttpGet("wiki/articles")]
    public async Task<IActionResult> ListWikiArticles([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
        => Ok(await _admin.ListWikiArticlesAsync(page, pageSize, search));

    [HttpDelete("wiki/articles/{id}")]
    public async Task<IActionResult> DeleteWikiArticle(Guid id)
    {
        var deleted = await _admin.DeleteWikiArticleAsync(id);
        return deleted ? Ok(new { success = true }) : NotFound(new { success = false, error = "Article not found" });
    }

    // ─── Wiki Categories ───

    [HttpGet("wiki/categories")]
    public async Task<IActionResult> ListWikiCategories([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
        => Ok(await _admin.ListWikiCategoriesAsync(page, pageSize, search));

    [HttpPost("wiki/categories")]
    public async Task<IActionResult> CreateWikiCategory([FromBody] WikiCategoryRequest req)
        => Ok(await _admin.CreateWikiCategoryAsync(req));

    [HttpPut("wiki/categories/{id}")]
    public async Task<IActionResult> UpdateWikiCategory(int id, [FromBody] WikiCategoryRequest req)
    {
        var result = await _admin.UpdateWikiCategoryAsync(id, req);
        return result != null ? Ok(result) : NotFound(new { success = false, error = "Category not found" });
    }

    [HttpDelete("wiki/categories/{id}")]
    public async Task<IActionResult> DeleteWikiCategory(int id)
    {
        var (found, hasArticles) = await _admin.DeleteWikiCategoryAsync(id);
        if (!found) return NotFound(new { success = false, error = "Category not found" });
        if (hasArticles) return BadRequest(new { success = false, error = "Cannot delete category with articles. Move or delete articles first." });
        return Ok(new { success = true });
    }

    // ─── Forum Threads ───

    [HttpGet("forum/threads")]
    public async Task<IActionResult> ListForumThreads([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
        => Ok(await _admin.ListForumThreadsAsync(page, pageSize, search));

    [HttpPut("forum/threads/{id}/pin")]
    public async Task<IActionResult> TogglePin(Guid id)
    {
        var result = await _admin.TogglePinAsync(id);
        return result != null ? Ok(result) : NotFound(new { success = false, error = "Thread not found" });
    }

    [HttpPut("forum/threads/{id}/lock")]
    public async Task<IActionResult> ToggleLock(Guid id)
    {
        var result = await _admin.ToggleLockAsync(id);
        return result != null ? Ok(result) : NotFound(new { success = false, error = "Thread not found" });
    }

    [HttpDelete("forum/threads/{id}")]
    public async Task<IActionResult> DeleteThread(Guid id)
    {
        var deleted = await _admin.DeleteThreadAsync(id);
        return deleted ? Ok(new { success = true }) : NotFound(new { success = false, error = "Thread not found" });
    }

    // ─── Forum Posts (Moderation) ───

    [HttpGet("forum/posts")]
    public async Task<IActionResult> ListForumPosts([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool? removedOnly = null, [FromQuery] string? search = null)
        => Ok(await _admin.ListForumPostsAsync(page, pageSize, removedOnly, search));

    [HttpPost("forum/posts/{id}/remove")]
    public async Task<IActionResult> RemovePost(Guid id, [FromBody] RemovePostRequest req)
    {
        var adminId = Guid.Parse(User.FindFirstValue("sub")!);
        var removed = await _admin.RemovePostAsync(id, adminId, req.Reason);
        return removed ? Ok(new { success = true }) : NotFound(new { success = false, error = "Post not found" });
    }

    [HttpPost("forum/posts/{id}/restore")]
    public async Task<IActionResult> RestorePost(Guid id)
    {
        var restored = await _admin.RestorePostAsync(id);
        return restored ? Ok(new { success = true }) : NotFound(new { success = false, error = "Post not found" });
    }

    // ─── Stats ───

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
        => Ok(await _admin.GetStatsAsync());
}