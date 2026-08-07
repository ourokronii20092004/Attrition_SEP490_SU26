using System.Linq.Expressions;
using System.Text.RegularExpressions;
using BuildingBlocks.Caching;
using BuildingBlocks.Contracts;
using BuildingBlocks.Web;
using Forum.Service.Clients;
using Forum.Service.DTOs;
using Forum.Service.Models;
using Forum.Service.Repositories;

namespace Forum.Service.Services;

public class ForumService : IForumService
{
    private readonly IForumRepository _threadRepo;
    private readonly ICacheService _cache;
    private readonly NotificationClient _notify;
    private readonly IdentityClient _identity;

    public ForumService(
        IForumRepository threadRepo,
        ICacheService cache,
        NotificationClient notify,
        IdentityClient identity)
    {
        _threadRepo = threadRepo;
        _cache = cache;
        _notify = notify;
        _identity = identity;
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
        Expression<Func<ForumPost, bool>>? filter = null;
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
            t.RootPostId == null &&
            !t.Moderation.IsRemoved &&
            t.WikiArticleId == null &&
            (categoryId == null || t.CategoryId == categoryId.Value) &&
            (s == null || (t.Title != null && t.Title.ToLower().Contains(s))) &&
            (authorId == null || t.AuthorId == authorId.Value);

        var (items, total) = await _threadRepo.GetPagedAsync(page, pageSize, filter,
            q => q.OrderByDescending(t => t.IsPinned).ThenByDescending(t => t.LastReplyAt));

        // Avatars aren't stored on the post; resolve them fresh from Identity (best-effort).
        var avatars = await _identity.ResolveUsersAsync(items.Select(t => t.AuthorId).ToList());
        var dtos = items.Select(t => new ForumThreadListDto(t.Id, t.Title ?? "Discussion", t.AuthorId,
            t.AuthorName ?? "Unknown",
            avatars.GetValueOrDefault(t.AuthorId)?.AvatarUrl ?? t.AuthorAvatar,
            t.IsPinned, t.IsLocked, t.ReplyCount, t.CreatedAt, t.LastReplyAt)).ToList();

        return new PaginatedResponse<ForumThreadListDto>(dtos, total, page, pageSize);
    }

    public async Task<ForumThreadDto?> GetThreadAsync(Guid threadId, Guid? currentUserId = null)
    {
        var thread = await _threadRepo.GetByIdAsync(threadId);
        if (thread == null || thread.RootPostId != null || thread.Moderation.IsRemoved) return null;

        var category = thread.CategoryId is { } categoryId ? await _threadRepo.GetCategoryByIdAsync(categoryId) : null;
        var reactions = await _threadRepo.Reactions.ListAsync(r => r.PostId == thread.Id);

        // Only look up the mute flag for a signed-in viewer; anonymous readers get no notifications
        // and so have nothing to mute.
        var isMuted = false;
        if (currentUserId is { } viewerId)
        {
            var (subs, _) = await _threadRepo.Subscriptions.GetPagedAsync(1, 1,
                ts => ts.ThreadId == threadId && ts.UserId == viewerId);
            isMuted = subs.FirstOrDefault()?.IsMuted ?? false;
        }

        // Same fresh-avatar resolve as the thread list and the reply list. Without it the opening
        // post is the one byline on the page rendering an initials fallback while its author's own
        // replies right below show their real picture: AuthorAvatar is only a write-time snapshot,
        // and it is null for every thread created before the avatar was set (or via a path that
        // never captured one).
        var threadAvatars = await _identity.ResolveUsersAsync(new[] { thread.AuthorId });

        return new ForumThreadDto(thread.Id, thread.Title ?? "Discussion", category?.Slug ?? string.Empty,
            thread.AuthorId, thread.AuthorName ?? "Unknown", thread.IsPinned, thread.IsLocked,
            thread.ReplyCount, thread.CreatedAt, thread.Content, ParseAttachments(thread.Attachments),
            threadAvatars.GetValueOrDefault(thread.AuthorId)?.AvatarUrl ?? thread.AuthorAvatar,
            thread.AuthorRole, thread.UpdatedAt,
            reactions.Count(r => r.ReactionType == ReactionType.Like),
            reactions.Count(r => r.ReactionType == ReactionType.Dislike),
            currentUserId.HasValue ? reactions.FirstOrDefault(r => r.UserId == currentUserId.Value)?.ReactionType : null,
            isMuted);
    }

    public async Task<ApiResponse<ForumThreadDto>> GetOrCreateWikiThreadAsync(Guid articleId, string articleTitle, Guid? currentUserId = null)
    {
        // QOLF-3b: one comment thread per article, created lazily on first view. Unlike a forum
        // thread it has no "first post" (the article is the content) and no category.
        var existing = await _threadRepo.GetByWikiArticleIdAsync(articleId);
        if (existing == null)
        {
            var thread = new ForumPost
            {
                CategoryId = null,
                Title = articleTitle,
                Content = string.Empty,
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

        // Same mute lookup as a forum thread, so the article's comment header can offer the toggle.
        var isMuted = false;
        if (currentUserId is { } viewerId)
        {
            var (subs, _) = await _threadRepo.Subscriptions.GetPagedAsync(1, 1,
                ts => ts.ThreadId == existing.Id && ts.UserId == viewerId);
            isMuted = subs.FirstOrDefault()?.IsMuted ?? false;
        }

        return ApiResponse<ForumThreadDto>.Ok(new ForumThreadDto(existing.Id, existing.Title ?? "Article comments", string.Empty,
            existing.AuthorId, existing.AuthorName ?? "Wiki", existing.IsPinned, existing.IsLocked,
            existing.ReplyCount, existing.CreatedAt, existing.Content, ParseAttachments(existing.Attachments),
            existing.AuthorAvatar, existing.AuthorRole, existing.UpdatedAt, 0, 0, null, isMuted));
    }

    public async Task<PaginatedResponse<ForumPostDto>> GetPostsAsync(Guid threadId, int page, int pageSize, Guid? currentUserId)
    {
        var (posts, total) = await _threadRepo.Posts.GetPagedAsync(page, pageSize,
            p => p.RootPostId == threadId && !p.Moderation.IsRemoved, q => q.OrderBy(p => p.CreatedAt));

        var postIds = posts.Select(p => p.Id).ToList();
        var reactions = await _threadRepo.Reactions.ListAsync(r => postIds.Contains(r.PostId));
        // Avatars aren't stored on the post; resolve them fresh from Identity (best-effort).
        var avatars = await _identity.ResolveUsersAsync(posts.Select(p => p.AuthorId).ToList());

        var items = posts.Select(p =>
        {
            var postReactions = reactions.Where(r => r.PostId == p.Id).ToList();
            return new ForumPostDto(p.Id, threadId, p.ParentPostId, p.Depth, p.AuthorId, p.AuthorName ?? "Unknown",
                avatars.GetValueOrDefault(p.AuthorId)?.AvatarUrl ?? p.AuthorAvatar, p.AuthorRole, p.Content, ParseAttachments(p.Attachments), p.CreatedAt, p.UpdatedAt,
                postReactions.Count(r => r.ReactionType == ReactionType.Like),
                postReactions.Count(r => r.ReactionType == ReactionType.Dislike),
                currentUserId.HasValue ? postReactions.FirstOrDefault(r => r.UserId == currentUserId.Value)?.ReactionType : null);
        }).ToList();

        return new PaginatedResponse<ForumPostDto>(items, total, page, pageSize);
    }

    public async Task<PaginatedResponse<UserReplyDto>> GetUserRepliesAsync(Guid userId, int page, int pageSize)
    {
        var (rows, total) = await _threadRepo.GetUserRepliesAsync(userId, page, pageSize);

        // Reaction counts are resolved here (same pattern as GetPostsAsync) rather than in the query.
        var postIds = rows.Select(r => r.PostId).ToList();
        var reactions = await _threadRepo.Reactions.ListAsync(r => postIds.Contains(r.PostId));

        var items = rows.Select(r => new UserReplyDto(
            r.PostId, r.ThreadId, r.ThreadTitle, r.Content, r.CreatedAt,
            reactions.Count(x => x.PostId == r.PostId && x.ReactionType == ReactionType.Like),
            reactions.Count(x => x.PostId == r.PostId && x.ReactionType == ReactionType.Dislike))).ToList();

        return new PaginatedResponse<UserReplyDto>(items, total, page, pageSize);
    }

    public async Task<ApiResponse<Guid>> CreateThreadAsync(CreateThreadRequest request, Author author)
    {
        var category = await _threadRepo.GetCategoryByIdAsync(request.CategoryId);
        if (category == null) return ApiResponse<Guid>.Fail("Category not found.");

        var thread = new ForumPost
        {
            CategoryId = request.CategoryId,
            Title = request.Title,
            AuthorId = author.Id,
            AuthorName = author.Name,
            AuthorAvatar = author.Avatar,
            AuthorRole = author.Role,
            Content = ContentSanitizer.Sanitize(request.Content)
        };

        var subscription = new ThreadSubscription { ThreadId = thread.Id, UserId = author.Id };

        await _threadRepo.CreateRootAsync(thread, subscription);
        await InvalidateCategoriesAsync();
        return ApiResponse<Guid>.Ok(thread.Id);
    }

    public async Task<ApiResponse<ForumPostDto>> CreatePostAsync(Guid threadId, CreatePostRequest request, Author author)
    {
        var thread = await _threadRepo.GetByIdAsync(threadId);
        if (thread == null || thread.RootPostId != null || thread.Moderation.IsRemoved)
            return ApiResponse<ForumPostDto>.Fail("Thread not found.");
        if (thread.IsLocked) return ApiResponse<ForumPostDto>.Fail("Thread is locked.");

        // Resolve nesting: a reply to a parent post inherits depth+1 (capped so the UI indent
        // stays sane — deeper replies still thread, just stop indenting). Parent must be in this thread.
        const int MaxDepth = 8;
        Guid? parentPostId = null;
        var depth = 0;
        Guid? parentAuthorId = null;
        if (request.ParentPostId is { } pid)
        {
            var parent = await _threadRepo.Posts.GetByIdAsync(pid);
            if (parent == null || (parent.Id != threadId && parent.RootPostId != threadId))
                return ApiResponse<ForumPostDto>.Fail("The post you're replying to no longer exists.");
            parentPostId = pid;
            depth = Math.Min(parent.Depth + 1, MaxDepth);
            parentAuthorId = parent.AuthorId;
        }

        var newPost = new ForumPost
        {
            RootPostId = threadId,
            ParentPostId = parentPostId,
            Depth = depth,
            AuthorId = author.Id,
            AuthorName = author.Name,
            AuthorAvatar = author.Avatar,
            AuthorRole = author.Role,
            Content = ContentSanitizer.Sanitize(request.Content),
            Attachments = SerializeAttachments(request.Attachments)
        };
        await _threadRepo.Posts.AddAsync(newPost);

        await _threadRepo.IncrementReplyCountAsync(threadId, DateTime.UtcNow);

        // Auto-subscribe the replier if they have no row yet. An existing row is left untouched,
        // so replying to a thread you muted does not quietly re-follow it.
        var (existing, _) = await _threadRepo.Subscriptions.GetPagedAsync(1, 1, sub => sub.ThreadId == threadId && sub.UserId == author.Id);
        if (existing.Count == 0)
            await _threadRepo.Subscriptions.AddAsync(new ThreadSubscription { ThreadId = threadId, UserId = author.Id });

        await InvalidateCategoriesAsync();

        // Dispatch notifications (fire-and-forget; never blocks/fails the post).
        // Deep-link to the exact post that triggered it (QOLF-4), not just the thread.
        var link = $"/forum/{threadId}#post-{newPost.Id}";

        // Fan out to everyone with a stake in this reply, most specific reason first. Each user is
        // notified at most once per reply — `sent` collapses the overlap (the thread author is
        // usually also a subscriber, and may be the parent author too). Guid.Empty is seeded in
        // because wiki-article threads carry a synthetic "Wiki" author that owns no account.
        var sent = new HashSet<Guid> { author.Id, Guid.Empty };

        // Anyone who muted this thread is excluded up front, so an opt-out holds even when the
        // person would otherwise be notified for a stronger reason — being the thread's author, or
        // the author of the post being replied to. Without this the mute button would appear to
        // work while notifications kept arriving.
        var subscriptions = await _threadRepo.Subscriptions.ListAsync(sub => sub.ThreadId == threadId);
        foreach (var sub in subscriptions)
            if (sub.IsMuted) sent.Add(sub.UserId);

        // The author of the post being replied to.
        if (parentAuthorId is { } pa && sent.Add(pa))
            await _notify.NotifyUserAsync(pa, NotifyType.Reply,
                $"{author.Name} replied to your post", link, author.Name, default);

        // The thread owner, when this is a direct comment on the thread. A top-level reply has no
        // parent post, so without this the owner is never told their thread was commented on. For
        // nested replies the owner is reached below as a subscriber, with wording that fits better.
        if (parentPostId is null && sent.Add(thread.AuthorId))
            await _notify.NotifyUserAsync(thread.AuthorId, NotifyType.Reply,
                $"{author.Name} commented on your post", link, author.Name, default);

        // Everyone else following the thread. Subscriptions were being written on every reply but
        // never read, so participants heard nothing about later activity. Sent as one bulk call so
        // a well-followed thread doesn't put a round-trip per subscriber on this request.
        var followers = new List<Guid>();
        foreach (var sub in subscriptions)
            if (!sub.IsMuted && sent.Add(sub.UserId)) followers.Add(sub.UserId);
        await _notify.NotifyUsersAsync(followers, NotifyType.Reply,
            $"{author.Name} replied in a thread you follow", link, author.Name, default);

        // @mentions: notify each distinct mentioned username (skip self-mention).
        var mentioned = MentionPattern.Matches(request.Content)
            .Select(m => m.Groups[1].Value)
            .Where(u => !string.Equals(u, author.Name, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var username in mentioned)
            await _notify.NotifyUsernameAsync(username, NotifyType.Mention,
                $"{author.Name} mentioned you in a post", link, author.Name, default);

        // Resolve the avatar the same way the read paths do, so the reply the client renders from
        // this response matches what it will show after the next refetch (the stored snapshot is
        // null whenever the caller's token carried no avatar claim).
        var authorAvatars = await _identity.ResolveUsersAsync(new[] { newPost.AuthorId });

        return ApiResponse<ForumPostDto>.Ok(new ForumPostDto(newPost.Id, threadId, newPost.ParentPostId,
            newPost.Depth, newPost.AuthorId, newPost.AuthorName ?? "Unknown",
            authorAvatars.GetValueOrDefault(newPost.AuthorId)?.AvatarUrl ?? newPost.AuthorAvatar,
            newPost.AuthorRole, newPost.Content, ParseAttachments(newPost.Attachments), newPost.CreatedAt,
            newPost.UpdatedAt, 0, 0, null));
    }

    public async Task<ApiResponse> UpdatePostAsync(Guid postId, UpdatePostRequest request, Guid userId)
    {
        var post = await _threadRepo.Posts.GetByIdAsync(postId);
        if (post == null) return ApiResponse.Fail("Post not found.");
        if (post.AuthorId != userId) return ApiResponse.Fail("Unauthorized.");

        post.Content = ContentSanitizer.Sanitize(request.Content);
        post.UpdatedAt = DateTime.UtcNow;
        await _threadRepo.Posts.UpdateAsync(post);
        await InvalidateCategoriesAsync();
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> DeletePostAsync(Guid postId, Guid userId, bool isAdmin)
    {
        var post = await _threadRepo.Posts.GetByIdAsync(postId);
        if (post == null) return ApiResponse.Fail("Post not found.");
        if (post.AuthorId != userId && !isAdmin) return ApiResponse.Fail("Unauthorized.");

        await _threadRepo.DeletePostCascadeAsync(post);
        await InvalidateCategoriesAsync();
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> ToggleReactionAsync(Guid postId, Guid userId, ReactRequest request)
    {
        // Without this, a reaction to a non-existent post is silently stored (no FK on the column).
        if (await _threadRepo.Posts.GetByIdAsync(postId) is null)
            return ApiResponse.Fail("Post not found.");

        var (existingList, _) = await _threadRepo.Reactions.GetPagedAsync(1, 1, r => r.PostId == postId && r.UserId == userId);
        var existing = existingList.FirstOrDefault();

        if (existing != null)
        {
            if (existing.ReactionType == request.ReactionType)
                await _threadRepo.Reactions.DeleteAsync(existing);
            else
            {
                existing.ReactionType = request.ReactionType;
                await _threadRepo.Reactions.UpdateAsync(existing);
            }
        }
        else
        {
            // Race-safe: a concurrent reaction from the same user hits the unique (PostId,UserId)
            // index; treat the duplicate as idempotent success rather than a 500.
            await _threadRepo.Reactions.TryAddAsync(new ForumReaction { PostId = postId, UserId = userId, ReactionType = request.ReactionType });
        }
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> SavePostAttachmentsAsync(Guid postId, List<string> urls, Guid userId)
    {
        var post = await _threadRepo.Posts.GetByIdAsync(postId);
        if (post == null) return ApiResponse.Fail("Post not found.");
        if (post.AuthorId != userId) return ApiResponse.Fail("Unauthorized.");

        post.Attachments = System.Text.Json.JsonSerializer.Serialize(urls);
        await _threadRepo.Posts.UpdateAsync(post);
        return ApiResponse.Ok();
    }

    /// <summary>
    /// Follow or mute a thread. Takes the desired state rather than toggling, so a double-click or
    /// a retried request can't land the user on the opposite setting from the one they clicked.
    ///
    /// Muting writes a row with IsMuted set, rather than deleting the subscription: replying
    /// auto-subscribes, so a deleted row would quietly re-follow the thread on the user's next post
    /// and the mute would never hold.
    /// </summary>
    public async Task<ApiResponse> SetThreadMutedAsync(Guid threadId, Guid userId, bool muted)
    {
        if (await _threadRepo.GetByIdAsync(threadId) is null)
            return ApiResponse.Fail("Thread not found.");

        var (existing, _) = await _threadRepo.Subscriptions.GetPagedAsync(1, 1, ts => ts.ThreadId == threadId && ts.UserId == userId);
        var sub = existing.FirstOrDefault();

        if (sub != null)
        {
            if (sub.IsMuted != muted)
            {
                sub.IsMuted = muted;
                await _threadRepo.Subscriptions.UpdateAsync(sub);
            }
            return new ApiResponse(true, muted ? "Muted this thread." : "Following this thread.");
        }

        // Race-safe against the unique (ThreadId,UserId) index; a duplicate is idempotent success.
        await _threadRepo.Subscriptions.TryAddAsync(new ThreadSubscription
        {
            ThreadId = threadId,
            UserId = userId,
            IsMuted = muted,
        });
        return new ApiResponse(true, muted ? "Muted this thread." : "Following this thread.");
    }

    public async Task<ApiResponse> ReportPostAsync(Guid postId, string reason, Author reporter)
    {
        var post = await _threadRepo.Posts.GetByIdAsync(postId);
        if (post == null) return ApiResponse.Fail("Post not found.");

        await _threadRepo.Reports.AddAsync(new PostReport
        {
            PostId = postId,
            ReporterId = reporter.Id,
            ReporterName = reporter.Name,
            Reason = reason,
            Status = ReportStatus.Pending
        });
        return new ApiResponse(true, "Post reported successfully.");
    }

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
        var post = await _threadRepo.Posts.GetByIdAsync(postId);
        if (post == null) return ApiResponse.Fail("Post not found.");

        post.Moderation.IsRemoved = true;
        post.Moderation.Reason = reason;
        post.Moderation.ByUserId = moderator.Id;
        post.Moderation.ByName = moderator.Name;
        post.Moderation.At = DateTime.UtcNow;
        await _threadRepo.Posts.UpdateAsync(post);

        // Resolved-after-action: pending reports on this post are now actioned.
        var pending = await _threadRepo.Reports.ListAsync(r => r.PostId == postId && r.Status == ReportStatus.Pending);
        foreach (var report in pending)
        {
            report.Status = ReportStatus.Resolved;
            await _threadRepo.Reports.UpdateAsync(report);
        }
        await InvalidateCategoriesAsync();
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> RestorePostAsync(Guid postId)
    {
        var post = await _threadRepo.Posts.GetByIdAsync(postId);
        if (post == null) return ApiResponse.Fail("Post not found.");

        post.Moderation.IsRemoved = false;
        post.Moderation.Reason = null;
        post.Moderation.ByUserId = null;
        post.Moderation.ByName = null;
        post.Moderation.At = null;
        await _threadRepo.Posts.UpdateAsync(post);
        return ApiResponse.Ok();
    }

    public async Task<PaginatedResponse<AdminForumThreadDto>> ListThreadsForModerationAsync(int page, int pageSize)
    {
        var (threads, total) = await _threadRepo.GetPagedAsync(page, pageSize, p => p.RootPostId == null,
            q => q.OrderByDescending(t => t.LastReplyAt));
        var items = threads.Select(t => new AdminForumThreadDto(t.Id, t.Title ?? "Discussion", t.IsPinned, t.IsLocked,
            t.ReplyCount, t.CreatedAt, t.LastReplyAt, t.AuthorName)).ToList();
        return new PaginatedResponse<AdminForumThreadDto>(items, total, page, pageSize);
    }

    public async Task<PaginatedResponse<AdminForumPostDto>> ListPostsForModerationAsync(bool removedOnly, string? search, int page, int pageSize)
    {
        Expression<Func<ForumPost, bool>>? filter = (removedOnly, search?.ToLower()) switch
        {
            (true, string s) => p => p.Moderation.IsRemoved && p.Content.ToLower().Contains(s),
            (true, null) => p => p.Moderation.IsRemoved,
            (false, string s) => p => p.Content.ToLower().Contains(s),
            _ => null
        };
        var (posts, total) = await _threadRepo.Posts.GetPagedAsync(page, pageSize, filter,
            q => q.OrderByDescending(p => p.CreatedAt));
        var items = posts.Select(p => new AdminForumPostDto(p.Id, p.RootPostId ?? p.Id, p.Content, p.CreatedAt, p.UpdatedAt,
            p.Moderation.IsRemoved, p.Moderation.Reason, p.Moderation.At, p.AuthorName, p.Moderation.ByName)).ToList();
        return new PaginatedResponse<AdminForumPostDto>(items, total, page, pageSize);
    }

    public async Task<PaginatedResponse<AdminPostReportDto>> ListReportsAsync(string status, int page, int pageSize)
    {
        var (reports, total) = await _threadRepo.Reports.GetPagedAsync(page, pageSize, r => r.Status == status,
            q => q.OrderByDescending(r => r.CreatedAt));
        var reportPostIds = reports.Select(r => r.PostId).Distinct().ToList();
        var reportPosts = (await _threadRepo.Posts.ListAsync(p => reportPostIds.Contains(p.Id)))
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
        var report = await _threadRepo.Reports.GetByIdAsync(reportId);
        if (report == null) return ApiResponse.Fail("Report not found.");
        report.Status = ReportStatus.Dismissed;
        await _threadRepo.Reports.UpdateAsync(report);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> ResolveReportAsync(Guid reportId)
    {
        var report = await _threadRepo.Reports.GetByIdAsync(reportId);
        if (report == null) return ApiResponse.Fail("Report not found.");
        report.Status = ReportStatus.Resolved;
        await _threadRepo.Reports.UpdateAsync(report);
        return ApiResponse.Ok();
    }

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
        if (!await _threadRepo.Categories.TryAddAsync(category))
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
            await _threadRepo.Categories.UpdateAsync(category);
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
        var threadCount = await _threadRepo.CountAsync(t => t.RootPostId == null && t.CategoryId == id);
        if (threadCount > 0)
            return ApiResponse.Fail("This category still has threads. Move or delete them first.");

        await _threadRepo.Categories.DeleteAsync(category);
        await InvalidateCategoriesAsync();
        return ApiResponse.Ok();
    }

    public async Task<List<ForumPostSearchDto>> SearchAsync(string query, int limit)
    {
        var threads = await _threadRepo.SearchThreadsAsync(query, limit);
        var results = new List<ForumPostSearchDto>();
        foreach (var t in threads)
        {
            var firstPost = await _threadRepo.GetFirstPostAsync(t.Id);
            var postId = firstPost?.PostId ?? t.Id;
            var body = firstPost?.Content ?? t.Title ?? string.Empty;
            var snippet = body.Length > 120 ? body[..120] : body;
            results.Add(new ForumPostSearchDto(postId, t.Id, t.Title ?? "Discussion", snippet));
        }
        return results;
    }

    public async Task<(int Threads, int Posts, int RemovedPosts)> GetStatsAsync()
    {
        var threads = await _threadRepo.CountAsync(p => p.RootPostId == null);
        var posts = await _threadRepo.Posts.CountAsync();
        var removed = await _threadRepo.Posts.CountAsync(p => p.Moderation.IsRemoved);
        return (threads, posts, removed);
    }
}
