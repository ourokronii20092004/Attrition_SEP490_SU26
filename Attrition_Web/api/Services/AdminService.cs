using Attrition.API.Data;
using Attrition.API.DTOs;
using Attrition.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Attrition.API.Services;

public class AdminService
{
    private readonly AppDbContext _db;

    public AdminService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<object> ListUsersAsync(int page, int pageSize, string? search, string sort)
    {
        var query = _db.Users.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            var match = Regex.Match(search.Trim(), @"^(role|email):\s*""?([^""]+)""?", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var tag = match.Groups[1].Value.ToLower();
                var term = match.Groups[2].Value.ToLower();
                if (tag == "role") query = query.Where(u => u.Role.ToLower() == term);
                else if (tag == "email") query = query.Where(u => u.Email != null && u.Email.ToLower().Contains(term));
            }
            else
            {
                query = query.Where(u => u.Username.Contains(search) || (u.Email != null && u.Email.Contains(search)));
            }
        }

        if (sort == "role")
            query = query.OrderBy(u => u.Role == "Admin" ? 0 : 1).ThenByDescending(u => u.JoinedAt);
        else
            query = query.OrderByDescending(u => u.JoinedAt);

        var totalCount = await query.CountAsync();
        var users = await query
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

        return new { success = true, data = users, totalCount, page, pageSize };
    }

    public async Task<object?> DeleteUserAsync(Guid id, Guid adminId)
    {
        if (id == adminId) return new { success = false, error = "Cannot delete your own account." };

        var user = await _db.Users.FindAsync(id);
        if (user == null) return null;

        // Cascade: reactions, posts, threads, wiki contributions
        var reactions = _db.ForumReactions.Where(r => r.UserId == id);
        _db.ForumReactions.RemoveRange(reactions);

        var posts = _db.ForumPosts.Where(p => p.AuthorId == id);
        _db.ForumPosts.RemoveRange(posts);

        var threads = _db.ForumThreads.Where(t => t.AuthorId == id);
        _db.ForumThreads.RemoveRange(threads);

        var wikiContributions = _db.WikiContributions.Where(c => c.ContributorId == id);
        _db.WikiContributions.RemoveRange(wikiContributions);

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return new { success = true };
    }

    public async Task<object> ListWikiArticlesAsync(int page, int pageSize, string? search)
    {
        var query = _db.WikiArticles.AsQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(a => a.Title.Contains(search) || a.Slug.Contains(search));

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

        return new { success = true, data = articles, totalCount, page, pageSize };
    }

    public async Task<bool> DeleteWikiArticleAsync(Guid id)
    {
        var article = await _db.WikiArticles.FindAsync(id);
        if (article == null) return false;

        _db.WikiArticles.Remove(article);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<object> ListWikiCategoriesAsync(int page, int pageSize, string? search)
    {
        var query = _db.WikiCategories.AsQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(c => c.Name.Contains(search) || (c.Description != null && c.Description.Contains(search)));

        var totalCount = await query.CountAsync();
        var categories = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id, c.Name, c.Slug, c.Description, c.IconUrl,
                ArticleCount = _db.WikiArticles.Count(a => a.CategoryId == c.Id)
            })
            .ToListAsync();

        return new { success = true, data = categories, totalCount, page, pageSize };
    }

    public async Task<object> CreateWikiCategoryAsync(WikiCategoryRequest req)
    {
        var category = new WikiCategory
        {
            Name = req.Name,
            Slug = req.Name.ToLower().Replace(" ", "-"),
            Description = req.Description,
            IconUrl = req.IconUrl
        };
        _db.WikiCategories.Add(category);
        await _db.SaveChangesAsync();
        return new { success = true, data = category };
    }

    public async Task<object?> UpdateWikiCategoryAsync(int id, WikiCategoryRequest req)
    {
        var category = await _db.WikiCategories.FindAsync(id);
        if (category == null) return null;

        category.Name = req.Name;
        category.Slug = req.Name.ToLower().Replace(" ", "-");
        category.Description = req.Description;
        category.IconUrl = req.IconUrl;

        await _db.SaveChangesAsync();
        return new { success = true, data = category };
    }

    public async Task<(bool found, bool hasArticles)> DeleteWikiCategoryAsync(int id)
    {
        var category = await _db.WikiCategories.FindAsync(id);
        if (category == null) return (false, false);

        var hasArticles = await _db.WikiArticles.AnyAsync(a => a.CategoryId == id);
        if (hasArticles) return (true, true);

        _db.WikiCategories.Remove(category);
        await _db.SaveChangesAsync();
        return (true, false);
    }

    public async Task<object> ListForumThreadsAsync(int page, int pageSize, string? search)
    {
        var query = _db.ForumThreads.AsQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(t => t.Title.Contains(search));

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

        return new { success = true, data = threads, totalCount, page, pageSize };
    }

    public async Task<object?> TogglePinAsync(Guid id)
    {
        var thread = await _db.ForumThreads.FindAsync(id);
        if (thread == null) return null;

        thread.IsPinned = !thread.IsPinned;
        await _db.SaveChangesAsync();
        return new { success = true, data = new { isPinned = thread.IsPinned } };
    }

    public async Task<object?> ToggleLockAsync(Guid id)
    {
        var thread = await _db.ForumThreads.FindAsync(id);
        if (thread == null) return null;

        thread.IsLocked = !thread.IsLocked;
        await _db.SaveChangesAsync();
        return new { success = true, data = new { isLocked = thread.IsLocked } };
    }

    public async Task<bool> DeleteThreadAsync(Guid id)
    {
        var thread = await _db.ForumThreads.FindAsync(id);
        if (thread == null) return false;

        // Delete all posts in thread
        var posts = await _db.ForumPosts.Where(p => p.ThreadId == id).ToListAsync();
        _db.ForumPosts.RemoveRange(posts);
        _db.ForumThreads.Remove(thread);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<object> ListForumPostsAsync(int page, int pageSize, bool? removedOnly, string? search)
    {
        var query = _db.ForumPosts.AsQueryable();
        if (removedOnly == true) query = query.Where(p => p.IsRemoved);
        if (!string.IsNullOrEmpty(search)) query = query.Where(p => p.Content.Contains(search));

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

        return new { success = true, data = posts, totalCount, page, pageSize };
    }

    public async Task<bool> RemovePostAsync(Guid id, Guid adminId, string reason)
    {
        var post = await _db.ForumPosts.FindAsync(id);
        if (post == null) return false;

        post.IsRemoved = true;
        post.RemovedReason = reason;
        post.RemovedByUserId = adminId;
        post.RemovedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RestorePostAsync(Guid id)
    {
        var post = await _db.ForumPosts.FindAsync(id);
        if (post == null) return false;

        post.IsRemoved = false;
        post.RemovedReason = null;
        post.RemovedByUserId = null;
        post.RemovedAt = null;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<object> GetStatsAsync()
    {
        return new
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
        };
    }
}
