using Attrition.API.Data;
using Attrition.API.DTOs;
using Attrition.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Attrition.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly AppDbContext _db;

    public AdminController(AuthService auth, AppDbContext db)
    {
        _auth = auth;
        _db = db;
    }

    // ─── Users ───

    [HttpGet("users")]
    public async Task<IActionResult> ListUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
    {
        var query = _db.Users.AsQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(u => u.Username.Contains(search) || (u.Email != null && u.Email.Contains(search)));

        var totalCount = await query.CountAsync();
        var users = await query
            .OrderByDescending(u => u.JoinedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id, u.Username, u.Email, u.DisplayName, u.Role,
                u.AvatarPath, u.GoogleAvatarUrl, u.AuthProvider,
                u.IsBanned, u.JoinedAt, u.LastLoginAt,
                u.PostCount, u.ContributionCount
            })
            .ToListAsync();

        return Ok(new { success = true, data = users, totalCount, page, pageSize });
    }

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

    // ─── Wiki Articles ───

    [HttpGet("wiki/articles")]
    public async Task<IActionResult> ListWikiArticles([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = _db.WikiArticles.AsQueryable();
        var totalCount = await query.CountAsync();
        var articles = await query
            .OrderByDescending(a => a.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id, a.Title, a.Slug,
                CategoryName = _db.WikiCategories.Where(c => c.Id == a.CategoryId).Select(c => c.Name).FirstOrDefault(),
                AuthorName = _db.Users.Where(u => u.Id == a.CreatedById).Select(u => u.Username).FirstOrDefault(),
                a.CreatedAt, a.UpdatedAt
            })
            .ToListAsync();

        return Ok(new { success = true, data = articles, totalCount, page, pageSize });
    }

    [HttpDelete("wiki/articles/{id}")]
    public async Task<IActionResult> DeleteWikiArticle(Guid id)
    {
        var article = await _db.WikiArticles.FindAsync(id);
        if (article == null) return NotFound(new { success = false, error = "Article not found" });

        _db.WikiArticles.Remove(article);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ─── Wiki Categories ───

    [HttpGet("wiki/categories")]
    public async Task<IActionResult> ListWikiCategories()
    {
        var categories = await _db.WikiCategories
            .Select(c => new
            {
                c.Id, c.Name, c.Slug, c.Description, c.IconUrl,
                ArticleCount = _db.WikiArticles.Count(a => a.CategoryId == c.Id)
            })
            .ToListAsync();
        return Ok(new { success = true, data = categories });
    }

    [HttpPost("wiki/categories")]
    public async Task<IActionResult> CreateWikiCategory([FromBody] WikiCategoryRequest req)
    {
        var category = new Models.WikiCategory
        {
            Name = req.Name,
            Slug = req.Name.ToLower().Replace(" ", "-"),
            Description = req.Description,
            IconUrl = req.IconUrl
        };
        _db.WikiCategories.Add(category);
        await _db.SaveChangesAsync();
        return Ok(new { success = true, data = category });
    }

    [HttpPut("wiki/categories/{id}")]
    public async Task<IActionResult> UpdateWikiCategory(int id, [FromBody] WikiCategoryRequest req)
    {
        var category = await _db.WikiCategories.FindAsync(id);
        if (category == null) return NotFound(new { success = false, error = "Category not found" });

        category.Name = req.Name;
        category.Slug = req.Name.ToLower().Replace(" ", "-");
        category.Description = req.Description;
        category.IconUrl = req.IconUrl;

        await _db.SaveChangesAsync();
        return Ok(new { success = true, data = category });
    }

    [HttpDelete("wiki/categories/{id}")]
    public async Task<IActionResult> DeleteWikiCategory(int id)
    {
        var category = await _db.WikiCategories.FindAsync(id);
        if (category == null) return NotFound(new { success = false, error = "Category not found" });

        var hasArticles = await _db.WikiArticles.AnyAsync(a => a.CategoryId == id);
        if (hasArticles) return BadRequest(new { success = false, error = "Cannot delete category with articles. Move or delete articles first." });

        _db.WikiCategories.Remove(category);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ─── Forum Threads ───

    [HttpGet("forum/threads")]
    public async Task<IActionResult> ListForumThreads([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = _db.ForumThreads.AsQueryable();
        var totalCount = await query.CountAsync();
        var threads = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id, t.Title, t.IsPinned, t.IsLocked,
                t.ReplyCount, t.CreatedAt, t.LastReplyAt,
                CategoryName = _db.ForumCategories.Where(c => c.Id == t.CategoryId).Select(c => c.Name).FirstOrDefault(),
                AuthorName = _db.Users.Where(u => u.Id == t.AuthorId).Select(u => u.Username).FirstOrDefault()
            })
            .ToListAsync();

        return Ok(new { success = true, data = threads, totalCount, page, pageSize });
    }

    [HttpPut("forum/threads/{id}/pin")]
    public async Task<IActionResult> TogglePin(Guid id)
    {
        var thread = await _db.ForumThreads.FindAsync(id);
        if (thread == null) return NotFound(new { success = false, error = "Thread not found" });

        thread.IsPinned = !thread.IsPinned;
        await _db.SaveChangesAsync();
        return Ok(new { success = true, data = new { isPinned = thread.IsPinned } });
    }

    [HttpPut("forum/threads/{id}/lock")]
    public async Task<IActionResult> ToggleLock(Guid id)
    {
        var thread = await _db.ForumThreads.FindAsync(id);
        if (thread == null) return NotFound(new { success = false, error = "Thread not found" });

        thread.IsLocked = !thread.IsLocked;
        await _db.SaveChangesAsync();
        return Ok(new { success = true, data = new { isLocked = thread.IsLocked } });
    }

    [HttpDelete("forum/threads/{id}")]
    public async Task<IActionResult> DeleteThread(Guid id)
    {
        var thread = await _db.ForumThreads.FindAsync(id);
        if (thread == null) return NotFound(new { success = false, error = "Thread not found" });

        // Delete all posts in thread
        var posts = await _db.ForumPosts.Where(p => p.ThreadId == id).ToListAsync();
        _db.ForumPosts.RemoveRange(posts);
        _db.ForumThreads.Remove(thread);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ─── Forum Posts (Moderation) ───

    [HttpGet("forum/posts")]
    public async Task<IActionResult> ListForumPosts([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool? removedOnly = null)
    {
        var query = _db.ForumPosts.AsQueryable();
        if (removedOnly == true) query = query.Where(p => p.IsRemoved);

        var totalCount = await query.CountAsync();
        var posts = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id, p.ThreadId, p.Content, p.CreatedAt, p.UpdatedAt,
                p.IsRemoved, p.RemovedReason, p.RemovedAt,
                AuthorName = _db.Users.Where(u => u.Id == p.AuthorId).Select(u => u.Username).FirstOrDefault(),
                ThreadTitle = _db.ForumThreads.Where(t => t.Id == p.ThreadId).Select(t => t.Title).FirstOrDefault(),
                RemovedByName = p.RemovedByUserId != null
                    ? _db.Users.Where(u => u.Id == p.RemovedByUserId).Select(u => u.Username).FirstOrDefault()
                    : null
            })
            .ToListAsync();

        return Ok(new { success = true, data = posts, totalCount, page, pageSize });
    }

    [HttpPost("forum/posts/{id}/remove")]
    public async Task<IActionResult> RemovePost(Guid id, [FromBody] RemovePostRequest req)
    {
        var post = await _db.ForumPosts.FindAsync(id);
        if (post == null) return NotFound(new { success = false, error = "Post not found" });

        var adminId = Guid.Parse(User.FindFirstValue("sub")!);
        post.IsRemoved = true;
        post.RemovedReason = req.Reason;
        post.RemovedByUserId = adminId;
        post.RemovedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpPost("forum/posts/{id}/restore")]
    public async Task<IActionResult> RestorePost(Guid id)
    {
        var post = await _db.ForumPosts.FindAsync(id);
        if (post == null) return NotFound(new { success = false, error = "Post not found" });

        post.IsRemoved = false;
        post.RemovedReason = null;
        post.RemovedByUserId = null;
        post.RemovedAt = null;

        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ─── Stats ───

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        return Ok(new
        {
            success = true,
            data = new
            {
                totalUsers = await _db.Users.CountAsync(),
                totalWikiArticles = await _db.WikiArticles.CountAsync(),
                totalForumThreads = await _db.ForumThreads.CountAsync(),
                totalForumPosts = await _db.ForumPosts.CountAsync(),
                pendingContributions = await _db.WikiContributions.CountAsync(c => c.Status == "Pending"),
                totalMusicAlbums = await _db.MusicAlbums.CountAsync(),
                totalMusicTracks = await _db.MusicTracks.CountAsync(),
                removedPosts = await _db.ForumPosts.CountAsync(p => p.IsRemoved)
            }
        });
    }
}

// ─── Request DTOs ───

public record WikiCategoryRequest(string Name, string? Description, string? IconUrl);
public record RemovePostRequest(string Reason);