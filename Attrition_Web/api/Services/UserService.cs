using Attrition.API.Data;
using Attrition.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Attrition.API.Services;

public class UserService
{
    private readonly AppDbContext _db;
    private readonly FileService _files;

    public UserService(AppDbContext db, FileService files)
    {
        _db = db;
        _files = files;
    }

    public async Task<PaginatedResponse<object>?> GetUserPostsAsync(string username, int page, int pageSize)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
        if (user == null) return null;

        var query = _db.ForumPosts
            .Where(p => p.AuthorId == user.Id)
            .Join(_db.ForumThreads, p => p.ThreadId, t => t.Id, (p, t) => new { Post = p, ThreadTitle = t.Title })
            .OrderByDescending(x => x.Post.CreatedAt);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => (object)new {
                Id = x.Post.Id, ThreadId = x.Post.ThreadId, Content = x.Post.Content, CreatedAt = x.Post.CreatedAt,
                ThreadTitle = x.ThreadTitle
            })
            .ToListAsync();

        return new PaginatedResponse<object>(items, total, page, pageSize);
    }

    public async Task<PaginatedResponse<object>?> GetUserContributionsAsync(string username, int page, int pageSize)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
        if (user == null) return null;

        var query = _db.WikiContributions
            .Where(c => c.ContributorId == user.Id)
            .Join(_db.WikiArticles, c => c.ArticleId, a => a.Id, (c, a) => new { Contribution = c, ArticleTitle = a.Title, ArticleSlug = a.Slug })
            .OrderByDescending(x => x.Contribution.SubmittedAt);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => (object)new {
                Id = x.Contribution.Id, ArticleId = x.Contribution.ArticleId, Notes = x.Contribution.ChangeNote, CreatedAt = x.Contribution.SubmittedAt, Status = x.Contribution.Status,
                ArticleTitle = x.ArticleTitle, ArticleSlug = x.ArticleSlug
            })
            .ToListAsync();

        return new PaginatedResponse<object>(items, total, page, pageSize);
    }

    public async Task<ApiResponse> UpdateThemeAsync(Guid userId, string themeMode, string themeAccent)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return new ApiResponse(false, "User not found.");

        user.ThemeMode = themeMode;
        user.ThemeAccent = themeAccent;
        await _db.SaveChangesAsync();

        return new ApiResponse(true);
    }

    public async Task<ApiResponse> UpdateAvatarAsync(Guid userId, string? avatarPath)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user != null)
        {
            user.AvatarPath = avatarPath;
            await _db.SaveChangesAsync();
        }
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> DeleteAvatarAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user != null)
        {
            user.AvatarPath = null;
            await _db.SaveChangesAsync();
        }
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> UpdateBackgroundAsync(Guid userId, string? backgroundUrl)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user != null)
        {
            user.BackgroundUrl = backgroundUrl;
            await _db.SaveChangesAsync();
        }
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> DeleteBackgroundAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user != null)
        {
            user.BackgroundUrl = null;
            await _db.SaveChangesAsync();
        }
        return new ApiResponse(true);
    }
}
