using System.Linq.Expressions;
using System.Text.RegularExpressions;
using BuildingBlocks.Caching;
using BuildingBlocks.Contracts;
using BuildingBlocks.Persistence;
using BuildingBlocks.Web;
using Forum.Service.Clients;
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
    private readonly ICacheService _cache;
    private readonly NotificationClient _notify;

    public ForumService(
        IForumRepository threadRepo,
        IRepository<ForumCategory> categoryRepo,
        IRepository<ForumPost> postRepo,
        IRepository<ForumReaction> reactionRepo,
        IRepository<ThreadSubscription> subRepo,
        IRepository<PostReport> reportRepo,
        ICacheService cache,
        NotificationClient notify)
    {
        _threadRepo = threadRepo;
        _categoryRepo = categoryRepo;
        _postRepo = postRepo;
        _reactionRepo = reactionRepo;
        _subRepo = subRepo;
        _reportRepo = reportRepo;
        _cache = cache;
        _notify = notify;
    }

    // @username tokens to notify (letters/digits/underscore — matching the username rule).
    private static readonly Regex MentionPattern = new(@"@([a-zA-Z0-9_]+)", RegexOptions.Compiled);

    public async Task<List<ForumCategoryDto>> GetCategoriesAsync()
    {
        // Category list + per-category thread counts: shown on the forum landing, rarely changes.
        return await _cache.GetOrSetAsync("categories", async () =>
        {
            var categories = await _threadRepo.GetCategoriesAsync();
            var stats = await _threadRepo.GetCategoryStatsAsync();
            var dtos = new List<ForumCategoryDto>();
            foreach (var c in categories)
            {
                var stat = stats.GetValueOrDefault(c.Id);
                dtos.Add(new ForumCategoryDto(c.Id, c.Name, c.Slug, c.Description, stat.ThreadCount,
                    stat.LatestActivity));
            }
            return dtos;
        }, TimeSpan.FromMinutes(5));
    }

    private Task InvalidateCategoriesAsync() => _cache.RemoveAsync("categories");

    // Attachments are stored as a JSON array of public image URLs. Cap count + only accept the
    // app's own media URLs (inline-image upload) so a post can't embed arbitrary external content.
    private static IReadOnlyList<string> ParseAttachments(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return System.Array.Empty<string>();
        try
        {
            var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(raw);
            return list ?? (IReadOnlyList<string>)System.Array.Empty<string>();
        }
        catch { return System.Array.Empty<string>(); }
    }

    private static string? SerializeAttachments(List<string>? urls)
    {
        if (urls == null || urls.Count == 0) return null;
        var clean = urls
            .Where(u => u.StartsWith("/api/", System.StringComparison.Ordinal))  // app-relative media only
            .Take(10)
            .ToList();
        return clean.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(clean);
    }

    public async Task<PaginatedResponse<ForumThreadListDto>> GetThreadsAsync(string? categorySlug, string? search, int page, int pageSize, Guid? authorId = null)
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
        // Compose the optional filters (category, search, author) into one predicate. Wiki-comment
        // threads (WikiArticleId != null) are excluded — they're reached via the article (QOLF-3b).
        filter = t =>
            t.WikiArticleId == null &&
            (categoryId == null || t.CategoryId == categoryId.Value) &&
            (s == null || t.Title.ToLower().Contains(s)) &&
            (authorId == null || t.AuthorId == authorId.Value);

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

    public async Task<ApiResponse<ForumThreadDto>> GetOrCreateWikiThreadAsync(Guid articleId, string articleTitle)
    {
        // QOLF-3b: one comment thread per article, created lazily on first view. Unlike a forum
        // thread it has no "first post" (the article is the content) and no category.
        var existing = await _threadRepo.GetByWikiArticleIdAsync(articleId);
        if (existing == null)
        {
            var thread = new ForumThread
            {
                CategoryId = 0,
                Title = articleTitle,
                AuthorId = Guid.Empty,
                AuthorName = "Wiki",
                WikiArticleId = articleId
            };
            // Race-safe against the filtered unique index; if a concurrent request won, re-fetch it.
            if (!await _threadRepo.TryAddAsync(thread))
                existing = await _threadRepo.GetByWikiArticleIdAsync(articleId);
            else
                existing = thread;
        }
        if (existing == null) return ApiResponse<ForumThreadDto>.Fail("Could not open the comment thread.");

        return ApiResponse<ForumThreadDto>.Ok(new ForumThreadDto(existing.Id, existing.Title, string.Empty,
            existing.AuthorId, existing.AuthorName ?? "Wiki", existing.IsPinned, existing.IsLocked,
            existing.ReplyCount, existing.CreatedAt));
    }

    public async Task<PaginatedResponse<ForumPostDto>> GetPostsAsync(Guid threadId, int page, int pageSize, Guid? currentUserId)
    {
        var (posts, total) = await _postRepo.GetPagedAsync(page, pageSize,
            p => p.ThreadId == threadId && !p.IsRemoved, q => q.OrderBy(p => p.CreatedAt));

        var postIds = posts.Select(p => p.Id).ToList();
        var reactions = await _reactionRepo.ListAsync(r => postIds.Contains(r.PostId));

        var items = posts.Select(p =>
        {
            var postReactions = reactions.Where(r => r.PostId == p.Id).ToList();
            return new ForumPostDto(p.Id, p.ThreadId, p.ParentPostId, p.Depth, p.AuthorId, p.AuthorName ?? "Unknown",
                p.AuthorAvatar, p.AuthorRole, p.Content, ParseAttachments(p.Attachments), p.CreatedAt, p.UpdatedAt,
                postReactions.Count(r => r.ReactionType == ReactionType.Like),
                postReactions.Count(r => r.ReactionType == ReactionType.Dislike),
                currentUserId.HasValue ? postReactions.FirstOrDefault(r => r.UserId == currentUserId.Value)?.ReactionType : null);
        }).ToList();

        return new PaginatedResponse<ForumPostDto>(items, total, page, pageSize);
    }

    public async Task<ApiResponse<Guid>> CreateThreadAsync(CreateThreadRequest request, Author author)
    {
        var category = await _threadRepo.GetCategoryByIdAsync(request.CategoryId);
        if (category == null) return ApiResponse<Guid>.Fail("Category not found.");

        var thread = new ForumThread
        {
            CategoryId = request.CategoryId,
            Title = request.Title,
            AuthorId = author.Id,
            AuthorName = author.Name,
            AuthorAvatar = author.Avatar
        };

        var firstPost = new ForumPost
        {
            ThreadId = thread.Id,
            AuthorId = author.Id,
            AuthorName = author.Name,
            AuthorAvatar = author.Avatar,
            AuthorRole = author.Role,
            Content = ContentSanitizer.Sanitize(request.Content)
        };

        var subscription = new ThreadSubscription { ThreadId = thread.Id, UserId = author.Id };

        await _threadRepo.CreateThreadWithFirstPostAsync(thread, firstPost, subscription);
        await InvalidateCategoriesAsync();
        return ApiResponse<Guid>.Ok(thread.Id);
    }

    public async Task<ApiResponse> CreatePostAsync(Guid threadId, CreatePostRequest request, Author author)
    {
        var thread = await _threadRepo.GetByIdAsync(threadId);
        if (thread == null) return ApiResponse.Fail("Thread not found.");
        if (thread.IsLocked) return ApiResponse.Fail("Thread is locked.");

        // Resolve nesting: a reply to a parent post inherits depth+1 (capped so the UI indent
        // stays sane — deeper replies still thread, just stop indenting). Parent must be in this thread.
        const int MaxDepth = 8;
        Guid? parentPostId = null;
        var depth = 0;
        Guid? parentAuthorId = null;
        if (request.ParentPostId is { } pid)
        {
            var parent = await _postRepo.GetByIdAsync(pid);
            if (parent == null || parent.ThreadId != threadId)
                return ApiResponse.Fail("The post you're replying to no longer exists.");
            parentPostId = pid;
            depth = Math.Min(parent.Depth + 1, MaxDepth);
            parentAuthorId = parent.AuthorId;
        }

        var newPost = new ForumPost
        {
            ThreadId = threadId,
            ParentPostId = parentPostId,
            Depth = depth,
            AuthorId = author.Id,
            AuthorName = author.Name,
            AuthorAvatar = author.Avatar,
            AuthorRole = author.Role,
            Content = ContentSanitizer.Sanitize(request.Content),
            Attachments = SerializeAttachments(request.Attachments)
        };
        await _postRepo.AddAsync(newPost);

        await _threadRepo.IncrementReplyCountAsync(threadId, DateTime.UtcNow);

        // Auto-subscribe the replier if not already subscribed.
        var (existing, _) = await _subRepo.GetPagedAsync(1, 1, sub => sub.ThreadId == threadId && sub.UserId == author.Id);
        if (existing.Count == 0)
            await _subRepo.AddAsync(new ThreadSubscription { ThreadId = threadId, UserId = author.Id });

        // NOTE: reply-notification emails dropped — Forum no longer has access to subscriber emails
        // (lives in Identity now). Subscriptions are kept as data for a future notification service.
        await InvalidateCategoriesAsync();

        // Dispatch notifications (fire-and-forget; never blocks/fails the post).
        // Deep-link to the exact post that triggered it (QOLF-4), not just the thread.
        var link = $"/forum/{threadId}#post-{newPost.Id}";
        // Reply: notify the parent post's author, unless replying to oneself.
        if (parentAuthorId is { } pa && pa != author.Id)
            await _notify.NotifyUserAsync(pa, "reply",
                $"{author.Name} replied to your post", link, author.Name, default);

        // @mentions: notify each distinct mentioned username (skip self-mention).
        var mentioned = MentionPattern.Matches(request.Content)
            .Select(m => m.Groups[1].Value)
            .Where(u => !string.Equals(u, author.Name, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var username in mentioned)
            await _notify.NotifyUsernameAsync(username, "mention",
                $"{author.Name} mentioned you in a post", link, author.Name, default);

        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> UpdatePostAsync(Guid postId, UpdatePostRequest request, Guid userId)
    {
        var post = await _postRepo.GetByIdAsync(postId);
        if (post == null) return ApiResponse.Fail("Post not found.");
        if (post.AuthorId != userId) return ApiResponse.Fail("Unauthorized.");

        post.Content = ContentSanitizer.Sanitize(request.Content);
        post.UpdatedAt = DateTime.UtcNow;
        await _postRepo.UpdateAsync(post);
        await InvalidateCategoriesAsync();
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> DeletePostAsync(Guid postId, Guid userId, bool isAdmin)
    {
        var post = await _postRepo.GetByIdAsync(postId);
        if (post == null) return ApiResponse.Fail("Post not found.");
        if (post.AuthorId != userId && !isAdmin) return ApiResponse.Fail("Unauthorized.");

        await _threadRepo.DeletePostCascadeAsync(post);
        await InvalidateCategoriesAsync();
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> ToggleReactionAsync(Guid postId, Guid userId, ReactRequest request)
    {
        // Without this, a reaction to a non-existent post is silently stored (no FK on the column).
        if (await _postRepo.GetByIdAsync(postId) is null)
            return ApiResponse.Fail("Post not found.");

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
            // Race-safe: a concurrent reaction from the same user hits the unique (PostId,UserId)
            // index; treat the duplicate as idempotent success rather than a 500.
            await _reactionRepo.TryAddAsync(new ForumReaction { PostId = postId, UserId = userId, ReactionType = request.ReactionType });
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
        if (await _threadRepo.GetByIdAsync(threadId) is null)
            return ApiResponse.Fail("Thread not found.");

        var (existing, _) = await _subRepo.GetPagedAsync(1, 1, ts => ts.ThreadId == threadId && ts.UserId == userId);
        var sub = existing.FirstOrDefault();

        if (sub != null)
        {
            await _subRepo.DeleteAsync(sub);
            return new ApiResponse(true, "Unsubscribed successfully.");
        }
        // Race-safe against the unique (ThreadId,UserId) index; a duplicate is idempotent success.
        await _subRepo.TryAddAsync(new ThreadSubscription { ThreadId = threadId, UserId = userId });
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
            Status = ReportStatus.Pending
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
        await _threadRepo.DeleteThreadCascadeAsync(threadId);
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

        // Resolved-after-action: pending reports on this post are now actioned.
        var pending = await _reportRepo.ListAsync(r => r.PostId == postId && r.Status == ReportStatus.Pending);
        foreach (var report in pending)
        {
            report.Status = ReportStatus.Resolved;
            await _reportRepo.UpdateAsync(report);
        }
        await InvalidateCategoriesAsync();
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

    public async Task<PaginatedResponse<AdminForumThreadDto>> ListThreadsForModerationAsync(int page, int pageSize)
    {
        var (threads, total) = await _threadRepo.GetPagedAsync(page, pageSize, null,
            q => q.OrderByDescending(t => t.LastReplyAt));
        var items = threads.Select(t => new AdminForumThreadDto(t.Id, t.Title, t.IsPinned, t.IsLocked,
            t.ReplyCount, t.CreatedAt, t.LastReplyAt, t.AuthorName)).ToList();
        return new PaginatedResponse<AdminForumThreadDto>(items, total, page, pageSize);
    }

    public async Task<PaginatedResponse<AdminForumPostDto>> ListPostsForModerationAsync(bool removedOnly, string? search, int page, int pageSize)
    {
        Expression<Func<ForumPost, bool>>? filter = (removedOnly, search?.ToLower()) switch
        {
            (true, string s) => p => p.IsRemoved && p.Content.ToLower().Contains(s),
            (true, null) => p => p.IsRemoved,
            (false, string s) => p => p.Content.ToLower().Contains(s),
            _ => null
        };
        var (posts, total) = await _postRepo.GetPagedAsync(page, pageSize, filter,
            q => q.OrderByDescending(p => p.CreatedAt));
        var items = posts.Select(p => new AdminForumPostDto(p.Id, p.ThreadId, p.Content, p.CreatedAt, p.UpdatedAt,
            p.IsRemoved, p.RemovedReason, p.RemovedAt, p.AuthorName, p.RemovedByName)).ToList();
        return new PaginatedResponse<AdminForumPostDto>(items, total, page, pageSize);
    }

    public async Task<PaginatedResponse<AdminPostReportDto>> ListReportsAsync(string status, int page, int pageSize)
    {
        var (reports, total) = await _reportRepo.GetPagedAsync(page, pageSize, r => r.Status == status,
            q => q.OrderByDescending(r => r.CreatedAt));
        var reportPostIds = reports.Select(r => r.PostId).Distinct().ToList();
        var reportPosts = (await _postRepo.ListAsync(p => reportPostIds.Contains(p.Id)))
            .ToDictionary(p => p.Id);
        var items = new List<AdminPostReportDto>();
        foreach (var r in reports)
        {
            reportPosts.TryGetValue(r.PostId, out var post);
            items.Add(new AdminPostReportDto(r.Id, r.PostId, post?.Content ?? "(deleted)",
                post?.AuthorName ?? "Unknown", r.ReporterName ?? "Unknown", r.Reason, r.Status, r.CreatedAt));
        }
        return new PaginatedResponse<AdminPostReportDto>(items, total, page, pageSize);
    }

    public async Task<ApiResponse> DismissReportAsync(Guid reportId)
    {
        var report = await _reportRepo.GetByIdAsync(reportId);
        if (report == null) return ApiResponse.Fail("Report not found.");
        report.Status = ReportStatus.Dismissed;
        await _reportRepo.UpdateAsync(report);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> ResolveReportAsync(Guid reportId)
    {
        var report = await _reportRepo.GetByIdAsync(reportId);
        if (report == null) return ApiResponse.Fail("Report not found.");
        report.Status = ReportStatus.Resolved;
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
        // Optimistic check above for the message; TryAddAsync makes the unique-slug insert race-safe.
        if (!await _categoryRepo.TryAddAsync(category))
            return ApiResponse<int>.Fail("A category with a similar name already exists.");
        await InvalidateCategoriesAsync();
        return ApiResponse<int>.Ok(category.Id);
    }

    public async Task<ApiResponse> UpdateCategoryAsync(int id, ForumCategoryRequest request)
    {
        var category = await _threadRepo.GetCategoryByIdAsync(id);
        if (category == null) return ApiResponse.Fail("Category not found.");

        var slug = SlugHelper.GenerateSlug(request.Name);
        var clash = await _threadRepo.GetCategoryBySlugAsync(slug);
        if (clash != null && clash.Id != id)
            return ApiResponse.Fail("A category with a similar name already exists.");

        category.Name = request.Name;
        category.Slug = slug;
        category.Description = request.Description ?? string.Empty;
        try
        {
            await _categoryRepo.UpdateAsync(category);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            // Lost the slug race between the check above and save.
            return ApiResponse.Fail("A category with a similar name already exists.");
        }
        await InvalidateCategoriesAsync();
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> DeleteCategoryAsync(int id)
    {
        var category = await _threadRepo.GetCategoryByIdAsync(id);
        if (category == null) return ApiResponse.Fail("Category not found.");

        // Refuse to delete a category that still has threads — deleting it would orphan them.
        var threadCount = await _threadRepo.CountAsync(t => t.CategoryId == id);
        if (threadCount > 0)
            return ApiResponse.Fail("This category still has threads. Move or delete them first.");

        await _categoryRepo.DeleteAsync(category);
        await InvalidateCategoriesAsync();
        return ApiResponse.Ok();
    }

    // ─── Aggregator support ───
    public async Task<List<ForumPostSearchDto>> SearchAsync(string query, int limit)
    {
        var threads = await _threadRepo.SearchThreadsAsync(query, limit);
        var results = new List<ForumPostSearchDto>();
        foreach (var t in threads)
        {
            var firstPost = await _threadRepo.GetFirstPostAsync(t.Id);
            var postId = firstPost?.PostId ?? t.Id;
            var body = firstPost?.Content ?? t.Title;
            var snippet = body.Length > 120 ? body[..120] : body;
            results.Add(new ForumPostSearchDto(postId, t.Id, t.Title, snippet));
        }
        return results;
    }

    public async Task<(int Threads, int Posts, int RemovedPosts)> GetStatsAsync()
    {
        var threads = await _threadRepo.CountAsync();
        var posts = await _postRepo.CountAsync();
        var removed = await _postRepo.CountAsync(p => p.IsRemoved);
        return (threads, posts, removed);
    }
}
