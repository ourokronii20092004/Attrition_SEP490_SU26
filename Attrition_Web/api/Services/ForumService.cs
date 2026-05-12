using Attrition.API.Data;
using Attrition.API.DTOs;
using Attrition.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Attrition.API.Services;

public class ForumService
{
    private readonly AppDbContext _db;
    private readonly CacheService _cache;

    public ForumService(AppDbContext db, CacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<List<ForumCategoryDto>> GetCategoriesAsync()
    {
        return await _db.ForumCategories
            .OrderBy(c => c.SortOrder)
            .Select(c => new ForumCategoryDto(
                c.Id,
                c.Name,
                c.Slug,
                c.Description,
                _db.ForumThreads.Count(t => t.CategoryId == c.Id),
                _db.ForumThreads.Where(t => t.CategoryId == c.Id).Max(t => (DateTime?)t.LastReplyAt)
            ))
            .ToListAsync();
    }

    public async Task<PaginatedResponse<ForumThreadListDto>> GetThreadsAsync(string? categorySlug, string? search, int page, int pageSize)
    {
        var query = _db.ForumThreads.AsQueryable();

        if (!string.IsNullOrEmpty(categorySlug))
        {
            var categoryId = await _db.ForumCategories
                .Where(c => c.Slug == categorySlug)
                .Select(c => c.Id)
                .FirstOrDefaultAsync();
            query = query.Where(t => t.CategoryId == categoryId);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(t => t.Title.ToLower().Contains(search.ToLower()));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.IsPinned)
            .ThenByDescending(t => t.LastReplyAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new ForumThreadListDto(
                t.Id,
                t.Title,
                _db.Users.Where(u => u.Id == t.AuthorId).Select(u => u.Username).FirstOrDefault()!,
                _db.Users.Where(u => u.Id == t.AuthorId).Select(u => u.AvatarPath).FirstOrDefault(),
                t.IsPinned,
                t.IsLocked,
                t.ReplyCount,
                t.CreatedAt,
                t.LastReplyAt
            ))
            .ToListAsync();

        return new PaginatedResponse<ForumThreadListDto>(items, total, page, pageSize);
    }

    public async Task<ForumThreadDto?> GetThreadAsync(Guid threadId)
    {
        var thread = await _db.ForumThreads.FindAsync(threadId);
        if (thread == null) return null;

        var categorySlug = await _db.ForumCategories.Where(c => c.Id == thread.CategoryId).Select(c => c.Slug).FirstAsync();
        var authorName = await _db.Users.Where(u => u.Id == thread.AuthorId).Select(u => u.Username).FirstOrDefaultAsync();

        return new ForumThreadDto(
            thread.Id,
            thread.Title,
            categorySlug,
            authorName!,
            thread.IsPinned,
            thread.IsLocked,
            thread.ReplyCount,
            thread.CreatedAt
        );
    }

    public async Task<PaginatedResponse<ForumPostDto>> GetPostsAsync(Guid threadId, int page, int pageSize, Guid? currentUserId)
    {
        var query = _db.ForumPosts.Where(p => p.ThreadId == threadId).OrderBy(p => p.CreatedAt);
        var total = await query.CountAsync();

        var posts = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userIds = posts.Select(p => p.AuthorId).Distinct().ToList();
        var users = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);

        var postIds = posts.Select(p => p.Id).ToList();
        var reactions = await _db.ForumReactions.Where(r => postIds.Contains(r.PostId)).ToListAsync();

        var items = posts.Select(p =>
        {
            var user = users.GetValueOrDefault(p.AuthorId);
            var postReactions = reactions.Where(r => r.PostId == p.Id).ToList();
            
            return new ForumPostDto(
                p.Id,
                p.ThreadId,
                user?.Username ?? "Unknown",
                user?.AvatarPath,
                user?.Role ?? "User",
                p.Content,
                p.CreatedAt,
                p.UpdatedAt,
                postReactions.Count(r => r.ReactionType == "like"),
                postReactions.Count(r => r.ReactionType == "dislike"),
                currentUserId.HasValue ? postReactions.FirstOrDefault(r => r.UserId == currentUserId.Value)?.ReactionType : null
            );
        }).ToList();

        return new PaginatedResponse<ForumPostDto>(items, total, page, pageSize);
    }

    public async Task<ApiResponse<Guid>> CreateThreadAsync(CreateThreadRequest request, Guid userId)
    {
        var thread = new ForumThread
        {
            CategoryId = request.CategoryId,
            Title = request.Title,
            AuthorId = userId
        };
        _db.ForumThreads.Add(thread);

        var post = new ForumPost
        {
            ThreadId = thread.Id,
            AuthorId = userId,
            Content = request.Content
        };
        _db.ForumPosts.Add(post);

        var user = await _db.Users.FindAsync(userId);
        if (user != null) user.PostCount++;

        await _db.SaveChangesAsync();
        return new ApiResponse<Guid>(true, thread.Id);
    }

    public async Task<ApiResponse> CreatePostAsync(Guid threadId, CreatePostRequest request, Guid userId)
    {
        var thread = await _db.ForumThreads.FindAsync(threadId);
        if (thread == null) return new ApiResponse(false, "Thread not found.");
        if (thread.IsLocked) return new ApiResponse(false, "Thread is locked.");

        var post = new ForumPost
        {
            ThreadId = threadId,
            AuthorId = userId,
            Content = request.Content
        };
        _db.ForumPosts.Add(post);

        thread.ReplyCount++;
        thread.LastReplyAt = DateTime.UtcNow;

        var user = await _db.Users.FindAsync(userId);
        if (user != null) user.PostCount++;

        await _db.SaveChangesAsync();
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> UpdatePostAsync(Guid postId, UpdatePostRequest request, Guid userId)
    {
        var post = await _db.ForumPosts.FindAsync(postId);
        if (post == null) return new ApiResponse(false, "Post not found.");
        if (post.AuthorId != userId) return new ApiResponse(false, "Unauthorized.");

        post.Content = request.Content;
        post.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> DeletePostAsync(Guid postId, Guid userId, string role)
    {
        var post = await _db.ForumPosts.FindAsync(postId);
        if (post == null) return new ApiResponse(false, "Post not found.");

        if (post.AuthorId != userId && role != "Admin")
            return new ApiResponse(false, "Unauthorized.");

        _db.ForumPosts.Remove(post);

        var thread = await _db.ForumThreads.FindAsync(post.ThreadId);
        if (thread != null) thread.ReplyCount = Math.Max(0, thread.ReplyCount - 1);

        await _db.SaveChangesAsync();
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> ToggleReactionAsync(Guid postId, Guid userId, ReactRequest request)
    {
        var existing = await _db.ForumReactions
            .FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId);

        if (existing != null)
        {
            if (existing.ReactionType == request.ReactionType)
            {
                _db.ForumReactions.Remove(existing);
            }
            else
            {
                existing.ReactionType = request.ReactionType;
            }
        }
        else
        {
            _db.ForumReactions.Add(new ForumReaction
            {
                PostId = postId,
                UserId = userId,
                ReactionType = request.ReactionType
            });
        }

        await _db.SaveChangesAsync();
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> TogglePinAsync(Guid threadId)
    {
        var thread = await _db.ForumThreads.FindAsync(threadId);
        if (thread == null) return new ApiResponse(false, "Thread not found.");

        thread.IsPinned = !thread.IsPinned;
        await _db.SaveChangesAsync();
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> ToggleLockAsync(Guid threadId)
    {
        var thread = await _db.ForumThreads.FindAsync(threadId);
        if (thread == null) return new ApiResponse(false, "Thread not found.");

        thread.IsLocked = !thread.IsLocked;
        await _db.SaveChangesAsync();
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> DeleteThreadAsync(Guid threadId)
    {
        var thread = await _db.ForumThreads.FindAsync(threadId);
        if (thread == null) return new ApiResponse(false, "Thread not found.");

        _db.ForumThreads.Remove(thread);
        await _db.SaveChangesAsync();
        return new ApiResponse(true);
    }
}