using Attrition.API.DTOs;
using Attrition.API.Models;
using Attrition.API.Repositories;
using System.Linq.Expressions;

namespace Attrition.API.Services;

public class ForumService : IForumService
{
    private readonly IForumRepository _forumRepo;
    private readonly IRepository<ForumCategory> _forumCategoryRepo;
    private readonly IRepository<ForumPost> _forumPostRepo;
    private readonly IRepository<ForumReaction> _forumReactionRepo;
    private readonly IUserRepository _userRepo;
    private readonly ICacheService _cache;
    private readonly IRepository<ThreadSubscription> _subRepo;
    private readonly IRepository<PostReport> _reportRepo;
    private readonly IEmailService _emailService;

    public ForumService(
        IForumRepository forumRepo,
        IRepository<ForumCategory> forumCategoryRepo,
        IRepository<ForumPost> forumPostRepo,
        IRepository<ForumReaction> forumReactionRepo,
        IUserRepository userRepo,
        ICacheService cache,
        IRepository<ThreadSubscription> subRepo,
        IRepository<PostReport> reportRepo,
        IEmailService emailService)
    {
        _forumRepo = forumRepo;
        _forumCategoryRepo = forumCategoryRepo;
        _forumPostRepo = forumPostRepo;
        _forumReactionRepo = forumReactionRepo;
        _userRepo = userRepo;
        _cache = cache;
        _subRepo = subRepo;
        _reportRepo = reportRepo;
        _emailService = emailService;
    }


    public async Task<List<ForumCategoryDto>> GetCategoriesAsync()
    {
        var categories = await _forumCategoryRepo.GetAllAsync();
        var orderedCategories = categories.OrderBy(c => c.SortOrder).ToList();

        var dtos = new List<ForumCategoryDto>();
        foreach (var c in orderedCategories)
        {
            var threadCount = await _forumRepo.CountAsync(t => t.CategoryId == c.Id);
            
            var (threads, _) = await _forumRepo.GetPagedAsync(1, 1, t => t.CategoryId == c.Id, q => q.OrderByDescending(t => t.LastReplyAt));
            var latestReplyAt = threads.FirstOrDefault()?.LastReplyAt;

            dtos.Add(new ForumCategoryDto(c.Id, c.Name, c.Slug, c.Description, threadCount, latestReplyAt));
        }

        return dtos;
    }

    public async Task<PaginatedResponse<ForumThreadListDto>> GetThreadsAsync(string? categorySlug, string? search, int page, int pageSize)
    {
        Expression<Func<ForumThread, bool>>? filter = null;

        if (!string.IsNullOrEmpty(categorySlug))
        {
            var (categories, _) = await _forumCategoryRepo.GetPagedAsync(1, 1, c => c.Slug == categorySlug);
            var category = categories.FirstOrDefault();
            if (category != null)
            {
                if (!string.IsNullOrEmpty(search))
                    filter = t => t.CategoryId == category.Id && t.Title.ToLower().Contains(search.ToLower());
                else
                    filter = t => t.CategoryId == category.Id;
            }
            else
            {
                return new PaginatedResponse<ForumThreadListDto>(new List<ForumThreadListDto>(), 0, page, pageSize);
            }
        }
        else if (!string.IsNullOrEmpty(search))
        {
            filter = t => t.Title.ToLower().Contains(search.ToLower());
        }

        var (items, total) = await _forumRepo.GetPagedAsync(
            page, pageSize, filter,
            q => q.OrderByDescending(t => t.IsPinned).ThenByDescending(t => t.LastReplyAt)
        );

        var dtos = new List<ForumThreadListDto>();
        foreach (var t in items)
        {
            var author = await _userRepo.GetByIdAsync(t.AuthorId);
            dtos.Add(new ForumThreadListDto(
                t.Id,
                t.Title,
                author?.Username ?? "Unknown",
                author?.AvatarPath ?? author?.GoogleAvatarUrl,
                t.IsPinned,
                t.IsLocked,
                t.ReplyCount,
                t.CreatedAt,
                t.LastReplyAt
            ));
        }

        return new PaginatedResponse<ForumThreadListDto>(dtos, total, page, pageSize);
    }

    public async Task<ForumThreadDto?> GetThreadAsync(Guid threadId)
    {
        var thread = await _forumRepo.GetByIdAsync(threadId);
        if (thread == null) return null;

        var category = await _forumCategoryRepo.GetByIdAsync(thread.CategoryId);
        var author = await _userRepo.GetByIdAsync(thread.AuthorId);

        return new ForumThreadDto(
            thread.Id,
            thread.Title,
            category?.Slug ?? string.Empty,
            author?.Username ?? "Unknown",
            thread.IsPinned,
            thread.IsLocked,
            thread.ReplyCount,
            thread.CreatedAt
        );
    }

    public async Task<PaginatedResponse<ForumPostDto>> GetPostsAsync(Guid threadId, int page, int pageSize, Guid? currentUserId)
    {
        var (posts, total) = await _forumPostRepo.GetPagedAsync(
            page, pageSize,
            p => p.ThreadId == threadId,
            q => q.OrderBy(p => p.CreatedAt)
        );

        var userIds = posts.Select(p => p.AuthorId).Distinct().ToList();
        var (usersList, _) = await _userRepo.GetPagedAsync(1, int.MaxValue, u => userIds.Contains(u.Id));
        var users = usersList.ToDictionary(u => u.Id);

        var postIds = posts.Select(p => p.Id).ToList();
        var (reactionsList, _) = await _forumReactionRepo.GetPagedAsync(1, int.MaxValue, r => postIds.Contains(r.PostId));

        var items = posts.Select(p =>
        {
            var user = users.GetValueOrDefault(p.AuthorId);
            var postReactions = reactionsList.Where(r => r.PostId == p.Id).ToList();
            
            return new ForumPostDto(
                p.Id,
                p.ThreadId,
                user?.Username ?? "Unknown",
                user?.AvatarPath ?? user?.GoogleAvatarUrl,
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
        await _forumRepo.AddAsync(thread);

        var post = new ForumPost
        {
            ThreadId = thread.Id,
            AuthorId = userId,
            Content = request.Content
        };
        await _forumPostRepo.AddAsync(post);

        var user = await _userRepo.GetByIdAsync(userId);
        if (user != null)
        {
            user.PostCount++;
            await _userRepo.UpdateAsync(user);
        }

        // Auto-subscribe the author of the thread
        await _subRepo.AddAsync(new ThreadSubscription { ThreadId = thread.Id, UserId = userId });

        return new ApiResponse<Guid>(true, thread.Id);
    }

    public async Task<ApiResponse> CreatePostAsync(Guid threadId, CreatePostRequest request, Guid userId)
    {
        var thread = await _forumRepo.GetByIdAsync(threadId);
        if (thread == null) return new ApiResponse(false, "Thread not found.");
        if (thread.IsLocked) return new ApiResponse(false, "Thread is locked.");

        var post = new ForumPost
        {
            ThreadId = threadId,
            AuthorId = userId,
            Content = request.Content
        };
        await _forumPostRepo.AddAsync(post);

        thread.ReplyCount++;
        thread.LastReplyAt = DateTime.UtcNow;
        await _forumRepo.UpdateAsync(thread);

        var user = await _userRepo.GetByIdAsync(userId);
        if (user != null)
        {
            user.PostCount++;
            await _userRepo.UpdateAsync(user);
        }

        // Auto-subscribe the replier if not already subscribed
        var (existing, _) = await _subRepo.GetPagedAsync(1, 1, s => s.ThreadId == threadId && s.UserId == userId);
        if (existing.Count == 0)
        {
            await _subRepo.AddAsync(new ThreadSubscription { ThreadId = threadId, UserId = userId });
        }

        // Fetch all subscriptions for the thread to notify other users
        var (subs, _) = await _subRepo.GetPagedAsync(1, int.MaxValue, s => s.ThreadId == threadId);
        foreach (var sub in subs)
        {
            if (sub.UserId == userId) continue; // skip the person who replied

            var subscriber = await _userRepo.GetByIdAsync(sub.UserId);
            if (subscriber != null && subscriber.NotifyOnReply && !string.IsNullOrEmpty(subscriber.Email))
            {
                await _emailService.SendAsync(
                    subscriber.Email,
                    $"New reply in thread: {thread.Title}",
                    $"Hello {subscriber.Username},\n\nThere is a new reply in the thread '{thread.Title}' you are subscribed to.\n\nRead it here: http://localhost:3000/forum/thread/{threadId}");
            }
        }

        return new ApiResponse(true);
    }


    public async Task<ApiResponse> UpdatePostAsync(Guid postId, UpdatePostRequest request, Guid userId)
    {
        var post = await _forumPostRepo.GetByIdAsync(postId);
        if (post == null) return new ApiResponse(false, "Post not found.");
        if (post.AuthorId != userId) return new ApiResponse(false, "Unauthorized.");

        post.Content = request.Content;
        post.UpdatedAt = DateTime.UtcNow;

        await _forumPostRepo.UpdateAsync(post);
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> DeletePostAsync(Guid postId, Guid userId, string role)
    {
        var post = await _forumPostRepo.GetByIdAsync(postId);
        if (post == null) return new ApiResponse(false, "Post not found.");

        if (post.AuthorId != userId && role != "Admin")
            return new ApiResponse(false, "Unauthorized.");

        await _forumPostRepo.DeleteAsync(post);

        var thread = await _forumRepo.GetByIdAsync(post.ThreadId);
        if (thread != null)
        {
            thread.ReplyCount = Math.Max(0, thread.ReplyCount - 1);
            await _forumRepo.UpdateAsync(thread);
        }

        return new ApiResponse(true);
    }

    public async Task<ApiResponse> ToggleReactionAsync(Guid postId, Guid userId, ReactRequest request)
    {
        var (existingList, _) = await _forumReactionRepo.GetPagedAsync(1, 1, r => r.PostId == postId && r.UserId == userId);
        var existing = existingList.FirstOrDefault();

        if (existing != null)
        {
            if (existing.ReactionType == request.ReactionType)
            {
                await _forumReactionRepo.DeleteAsync(existing);
            }
            else
            {
                existing.ReactionType = request.ReactionType;
                await _forumReactionRepo.UpdateAsync(existing);
            }
        }
        else
        {
            await _forumReactionRepo.AddAsync(new ForumReaction
            {
                PostId = postId,
                UserId = userId,
                ReactionType = request.ReactionType
            });
        }

        return new ApiResponse(true);
    }

    public async Task<ApiResponse> TogglePinAsync(Guid threadId)
    {
        var thread = await _forumRepo.GetByIdAsync(threadId);
        if (thread == null) return new ApiResponse(false, "Thread not found.");

        thread.IsPinned = !thread.IsPinned;
        await _forumRepo.UpdateAsync(thread);
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> ToggleLockAsync(Guid threadId)
    {
        var thread = await _forumRepo.GetByIdAsync(threadId);
        if (thread == null) return new ApiResponse(false, "Thread not found.");

        thread.IsLocked = !thread.IsLocked;
        await _forumRepo.UpdateAsync(thread);
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> DeleteThreadAsync(Guid threadId)
    {
        var thread = await _forumRepo.GetByIdAsync(threadId);
        if (thread == null) return new ApiResponse(false, "Thread not found.");

        await _forumRepo.DeleteAsync(thread);
        return new ApiResponse(true);
    }

    public async Task<ApiResponse> SavePostAttachmentsAsync(Guid postId, List<string> urls, Guid userId)
    {
        var post = await _forumPostRepo.GetByIdAsync(postId);
        if (post == null) return new ApiResponse(false, "Post not found.");
        if (post.AuthorId != userId) return new ApiResponse(false, "Unauthorized.");

        post.Attachments = System.Text.Json.JsonSerializer.Serialize(urls);
        await _forumPostRepo.UpdateAsync(post);
        return new ApiResponse(true);
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
        else
        {
            var newSub = new ThreadSubscription { ThreadId = threadId, UserId = userId };
            await _subRepo.AddAsync(newSub);
            return new ApiResponse(true, "Subscribed successfully.");
        }
    }

    public async Task<ApiResponse> ReportPostAsync(Guid postId, string reason, Guid userId)
    {
        var post = await _forumPostRepo.GetByIdAsync(postId);
        if (post == null) return new ApiResponse(false, "Post not found.");

        var report = new PostReport
        {
            PostId = postId,
            ReporterId = userId,
            Reason = reason,
            Status = "Pending"
        };
        await _reportRepo.AddAsync(report);
        return new ApiResponse(true, "Post reported successfully.");
    }
}