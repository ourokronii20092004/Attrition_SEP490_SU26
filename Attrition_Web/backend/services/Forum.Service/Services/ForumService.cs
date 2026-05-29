using System.Linq.Expressions;
using BuildingBlocks.Contracts;
using BuildingBlocks.Persistence;
using Forum.Service.DTOs;
using Forum.Service.Models;
using Forum.Service.Repositories;

namespace Forum.Service.Services;

public class ForumService : IForumService
{
    private readonly IForumRepository _threadRepo;
    private readonly IRepository<ForumCategory> _categoryRepo;
    private readonly IRepository<ForumPost> _postRepo;
    private readonly IRepository<ForumReaction> _reactionRepo;
    private readonly IRepository<ThreadSubscription> _subRepo;
    private readonly IRepository<PostReport> _reportRepo;

    public ForumService(
        IForumRepository threadRepo,
        IRepository<ForumCategory> categoryRepo,
        IRepository<ForumPost> postRepo,
        IRepository<ForumReaction> reactionRepo,
        IRepository<ThreadSubscription> subRepo,
        IRepository<PostReport> reportRepo)
    {
        _threadRepo = threadRepo;
        _categoryRepo = categoryRepo;
        _postRepo = postRepo;
        _reactionRepo = reactionRepo;
        _subRepo = subRepo;
        _reportRepo = reportRepo;
    }

    public async Task<List<ForumCategoryDto>> GetCategoriesAsync()
    {
        var categories = await _threadRepo.GetCategoriesAsync();
        var dtos = new List<ForumCategoryDto>();
        foreach (var c in categories)
        {
            var threadCount = await _threadRepo.CountAsync(t => t.CategoryId == c.Id);
            var (threads, _) = await _threadRepo.GetPagedAsync(1, 1, t => t.CategoryId == c.Id,
                q => q.OrderByDescending(t => t.LastReplyAt));
            dtos.Add(new ForumCategoryDto(c.Id, c.Name, c.Slug, c.Description, threadCount,
                threads.FirstOrDefault()?.LastReplyAt));
        }
        return dtos;
    }

    public async Task<PaginatedResponse<ForumThreadListDto>> GetThreadsAsync(string? categorySlug, string? search, int page, int pageSize)
    {
        Expression<Func<ForumThread, bool>>? filter = null;
        int? categoryId = null;

        if (!string.IsNullOrEmpty(categorySlug))
        {
            var category = await _threadRepo.GetCategoryBySlugAsync(categorySlug);
            if (category == null)
                return new PaginatedResponse<ForumThreadListDto>(new List<ForumThreadListDto>(), 0, page, pageSize);
            categoryId = category.Id;
        }

        var s = search?.ToLower();
        filter = (categoryId, s) switch
        {
            (int cid, string q) => t => t.CategoryId == cid && t.Title.ToLower().Contains(q),
            (int cid, null) => t => t.CategoryId == cid,
            (null, string q) => t => t.Title.ToLower().Contains(q),
            _ => null
        };

        var (items, total) = await _threadRepo.GetPagedAsync(page, pageSize, filter,
            q => q.OrderByDescending(t => t.IsPinned).ThenByDescending(t => t.LastReplyAt));

        var dtos = items.Select(t => new ForumThreadListDto(t.Id, t.Title, t.AuthorId,
            t.AuthorName ?? "Unknown", t.AuthorAvatar, t.IsPinned, t.IsLocked, t.ReplyCount,
            t.CreatedAt, t.LastReplyAt)).ToList();

        return new PaginatedResponse<ForumThreadListDto>(dtos, total, page, pageSize);
    }

    public async Task<ForumThreadDto?> GetThreadAsync(Guid threadId)
    {
        var thread = await _threadRepo.GetByIdAsync(threadId);
        if (thread == null) return null;

        var category = await _threadRepo.GetCategoryByIdAsync(thread.CategoryId);
        return new ForumThreadDto(thread.Id, thread.Title, category?.Slug ?? string.Empty,
            thread.AuthorId, thread.AuthorName ?? "Unknown", thread.IsPinned, thread.IsLocked,
            thread.ReplyCount, thread.CreatedAt);
    }

    public async Task<PaginatedResponse<ForumPostDto>> GetPostsAsync(Guid threadId, int page, int pageSize, Guid? currentUserId)
    {
        var (posts, total) = await _postRepo.GetPagedAsync(page, pageSize,
            p => p.ThreadId == threadId && !p.IsRemoved, q => q.OrderBy(p => p.CreatedAt));

        var postIds = posts.Select(p => p.Id).ToList();
        var (reactions, _) = await _reactionRepo.GetPagedAsync(1, int.MaxValue, r => postIds.Contains(r.PostId));

        var items = posts.Select(p =>
        {
            var postReactions = reactions.Where(r => r.PostId == p.Id).ToList();
            return new ForumPostDto(p.Id, p.ThreadId, p.AuthorId, p.AuthorName ?? "Unknown",
                p.AuthorAvatar, p.AuthorRole, p.Content, p.CreatedAt, p.UpdatedAt,
                postReactions.Count(r => r.ReactionType == "like"),
                postReactions.Count(r => r.ReactionType == "dislike"),
                currentUserId.HasValue ? postReactions.FirstOrDefault(r => r.UserId == currentUserId.Value)?.ReactionType : null);
        }).ToList();

        return new PaginatedResponse<ForumPostDto>(items, total, page, pageSize);
    }

    public async Task<ApiResponse<Guid>> CreateThreadAsync(CreateThreadRequest request, Author author)
    {
        var thread = new ForumThread
        {
            CategoryId = request.CategoryId,
            Title = request.Title,
            AuthorId = author.Id,
            AuthorName = author.Name,
            AuthorAvatar = author.Avatar
        };
        await _threadRepo.AddAsync(thread);

        await _postRepo.AddAsync(new ForumPost
        {
            ThreadId = thread.Id,
            AuthorId = author.Id,
            AuthorName = author.Name,
            AuthorAvatar = author.Avatar,
            AuthorRole = author.Role,
            Content = request.Content
        });

        await _subRepo.AddAsync(new ThreadSubscription { ThreadId = thread.Id, UserId = author.Id });
        return ApiResponse<Guid>.Ok(thread.Id);
    }

    public async Task<ApiResponse> CreatePostAsync(Guid threadId, CreatePostRequest request, Author author)
    {
        var thread = await _threadRepo.GetByIdAsync(threadId);
        if (thread == null) return ApiResponse.Fail("Thread not found.");
        if (thread.IsLocked) return ApiResponse.Fail("Thread is locked.");

        await _postRepo.AddAsync(new ForumPost
        {
            ThreadId = threadId,
            AuthorId = author.Id,
            AuthorName = author.Name,
            AuthorAvatar = author.Avatar,
            AuthorRole = author.Role,
            Content = request.Content
        });

        thread.ReplyCount++;
        thread.LastReplyAt = DateTime.UtcNow;
        await _threadRepo.UpdateAsync(thread);

        // Auto-subscribe the replier if not already subscribed.
        var (existing, _) = await _subRepo.GetPagedAsync(1, 1, sub => sub.ThreadId == threadId && sub.UserId == author.Id);
        if (existing.Count == 0)
            await _subRepo.AddAsync(new ThreadSubscription { ThreadId = threadId, UserId = author.Id });

        // NOTE: reply-notification emails dropped — Forum no longer has access to subscriber emails
        // (lives in Identity now). Subscriptions are kept as data for a future notification service.
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> UpdatePostAsync(Guid postId, UpdatePostRequest request, Guid userId)
    {
        var post = await _postRepo.GetByIdAsync(postId);
        if (post == null) return ApiResponse.Fail("Post not found.");
        if (post.AuthorId != userId) return ApiResponse.Fail("Unauthorized.");

        post.Content = request.Content;
        post.UpdatedAt = DateTime.UtcNow;
        await _postRepo.UpdateAsync(post);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> DeletePostAsync(Guid postId, Guid userId, bool isAdmin)
    {
        var post = await _postRepo.GetByIdAsync(postId);
        if (post == null) return ApiResponse.Fail("Post not found.");
        if (post.AuthorId != userId && !isAdmin) return ApiResponse.Fail("Unauthorized.");

        await _postRepo.DeleteAsync(post);

        var thread = await _threadRepo.GetByIdAsync(post.ThreadId);
        if (thread != null)
        {
            thread.ReplyCount = Math.Max(0, thread.ReplyCount - 1);
            await _threadRepo.UpdateAsync(thread);
        }
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> ToggleReactionAsync(Guid postId, Guid userId, ReactRequest request)
    {
        var (existingList, _) = await _reactionRepo.GetPagedAsync(1, 1, r => r.PostId == postId && r.UserId == userId);
        var existing = existingList.FirstOrDefault();

        if (existing != null)
        {
            if (existing.ReactionType == request.ReactionType)
                await _reactionRepo.DeleteAsync(existing);
            else
            {
                existing.ReactionType = request.ReactionType;
                await _reactionRepo.UpdateAsync(existing);
            }
        }
        else
        {
            await _reactionRepo.AddAsync(new ForumReaction { PostId = postId, UserId = userId, ReactionType = request.ReactionType });
        }
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> SavePostAttachmentsAsync(Guid postId, List<string> urls, Guid userId)
    {
        var post = await _postRepo.GetByIdAsync(postId);
        if (post == null) return ApiResponse.Fail("Post not found.");
        if (post.AuthorId != userId) return ApiResponse.Fail("Unauthorized.");

        post.Attachments = System.Text.Json.JsonSerializer.Serialize(urls);
        await _postRepo.UpdateAsync(post);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> ToggleThreadSubscriptionAsync(Guid threadId, Guid userId)
    {
        var (existing, _) = await _subRepo.GetPagedAsync(1, 1, ts => ts.ThreadId == threadId && ts.UserId == userId);
        var sub = existing.FirstOrDefault();

        if (sub != null)
        {
            await _subRepo.DeleteAsync(sub);
            return new ApiResponse(true, "Unsubscribed successfully.");
        }
        await _subRepo.AddAsync(new ThreadSubscription { ThreadId = threadId, UserId = userId });
        return new ApiResponse(true, "Subscribed successfully.");
    }

    public async Task<ApiResponse> ReportPostAsync(Guid postId, string reason, Author reporter)
    {
        var post = await _postRepo.GetByIdAsync(postId);
        if (post == null) return ApiResponse.Fail("Post not found.");

        await _reportRepo.AddAsync(new PostReport
        {
            PostId = postId,
            ReporterId = reporter.Id,
            ReporterName = reporter.Name,
            Reason = reason,
            Status = "Pending"
        });
        return new ApiResponse(true, "Post reported successfully.");
    }

    // ─── Moderation ───
    public async Task<ApiResponse> TogglePinAsync(Guid threadId)
    {
        var thread = await _threadRepo.GetByIdAsync(threadId);
        if (thread == null) return ApiResponse.Fail("Thread not found.");
        thread.IsPinned = !thread.IsPinned;
        await _threadRepo.UpdateAsync(thread);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> ToggleLockAsync(Guid threadId)
    {
        var thread = await _threadRepo.GetByIdAsync(threadId);
        if (thread == null) return ApiResponse.Fail("Thread not found.");
        thread.IsLocked = !thread.IsLocked;
        await _threadRepo.UpdateAsync(thread);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> DeleteThreadAsync(Guid threadId)
    {
        var thread = await _threadRepo.GetByIdAsync(threadId);
        if (thread == null) return ApiResponse.Fail("Thread not found.");
        await _threadRepo.DeleteAsync(thread);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> RemovePostAsync(Guid postId, Author moderator, string reason)
    {
        var post = await _postRepo.GetByIdAsync(postId);
        if (post == null) return ApiResponse.Fail("Post not found.");

        post.IsRemoved = true;
        post.RemovedReason = reason;
        post.RemovedByUserId = moderator.Id;
        post.RemovedByName = moderator.Name;
        post.RemovedAt = DateTime.UtcNow;
        await _postRepo.UpdateAsync(post);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> RestorePostAsync(Guid postId)
    {
        var post = await _postRepo.GetByIdAsync(postId);
        if (post == null) return ApiResponse.Fail("Post not found.");

        post.IsRemoved = false;
        post.RemovedReason = null;
        post.RemovedByUserId = null;
        post.RemovedByName = null;
        post.RemovedAt = null;
        await _postRepo.UpdateAsync(post);
        return ApiResponse.Ok();
    }

    public async Task<List<AdminForumThreadDto>> ListThreadsForModerationAsync()
    {
        var (threads, _) = await _threadRepo.GetPagedAsync(1, int.MaxValue, null,
            q => q.OrderByDescending(t => t.LastReplyAt));
        return threads.Select(t => new AdminForumThreadDto(t.Id, t.Title, t.IsPinned, t.IsLocked,
            t.ReplyCount, t.CreatedAt, t.LastReplyAt, t.AuthorName)).ToList();
    }

    public async Task<List<AdminForumPostDto>> ListPostsForModerationAsync(bool removedOnly, string? search)
    {
        Expression<Func<ForumPost, bool>>? filter = (removedOnly, search?.ToLower()) switch
        {
            (true, string s) => p => p.IsRemoved && p.Content.ToLower().Contains(s),
            (true, null) => p => p.IsRemoved,
            (false, string s) => p => p.Content.ToLower().Contains(s),
            _ => null
        };
        var (posts, _) = await _postRepo.GetPagedAsync(1, 200, filter, q => q.OrderByDescending(p => p.CreatedAt));
        return posts.Select(p => new AdminForumPostDto(p.Id, p.ThreadId, p.Content, p.CreatedAt, p.UpdatedAt,
            p.IsRemoved, p.RemovedReason, p.RemovedAt, p.AuthorName, p.RemovedByName)).ToList();
    }

    public async Task<List<AdminPostReportDto>> ListReportsAsync(string status)
    {
        var (reports, _) = await _reportRepo.GetPagedAsync(1, int.MaxValue, r => r.Status == status,
            q => q.OrderByDescending(r => r.CreatedAt));
        var dtos = new List<AdminPostReportDto>();
        foreach (var r in reports)
        {
            var post = await _postRepo.GetByIdAsync(r.PostId);
            dtos.Add(new AdminPostReportDto(r.Id, r.PostId, post?.Content ?? "(deleted)",
                post?.AuthorName ?? "Unknown", r.ReporterName ?? "Unknown", r.Reason, r.Status, r.CreatedAt));
        }
        return dtos;
    }

    public async Task<ApiResponse> DismissReportAsync(Guid reportId)
    {
        var report = await _reportRepo.GetByIdAsync(reportId);
        if (report == null) return ApiResponse.Fail("Report not found.");
        report.Status = "Dismissed";
        await _reportRepo.UpdateAsync(report);
        return ApiResponse.Ok();
    }

    // ─── Category management ───
    public async Task<ApiResponse<int>> CreateCategoryAsync(ForumCategoryRequest request)
    {
        var slug = SlugHelper.GenerateSlug(request.Name);
        if (await _threadRepo.GetCategoryBySlugAsync(slug) != null)
            return ApiResponse<int>.Fail("A category with a similar name already exists.");

        var category = new ForumCategory
        {
            Name = request.Name,
            Slug = slug,
            Description = request.Description ?? string.Empty
        };
        await _categoryRepo.AddAsync(category);
        return ApiResponse<int>.Ok(category.Id);
    }

    public async Task<ApiResponse> UpdateCategoryAsync(int id, ForumCategoryRequest request)
    {
        var category = await _threadRepo.GetCategoryByIdAsync(id);
        if (category == null) return ApiResponse.Fail("Category not found.");
        category.Name = request.Name;
        category.Slug = SlugHelper.GenerateSlug(request.Name);
        category.Description = request.Description ?? string.Empty;
        await _categoryRepo.UpdateAsync(category);
        return ApiResponse.Ok();
    }

    // ─── Aggregator support ───
    public async Task<List<ForumPostSearchDto>> SearchAsync(string query, int limit)
    {
        var threads = await _threadRepo.SearchThreadsAsync(query, limit);
        return threads.Select(t => new ForumPostSearchDto(t.Id, t.Id, t.Title,
            t.Title.Length > 120 ? t.Title[..120] : t.Title)).ToList();
    }

    public async Task<(int Threads, int Posts, int RemovedPosts)> GetStatsAsync()
    {
        var threads = await _threadRepo.CountAsync();
        var posts = await _postRepo.CountAsync();
        var removed = await _postRepo.CountAsync(p => p.IsRemoved);
        return (threads, posts, removed);
    }
}
