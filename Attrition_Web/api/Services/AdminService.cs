using Attrition.API.DTOs;
using Attrition.API.Models;
using Attrition.API.Repositories;
using System.Text.RegularExpressions;
using System.Linq.Expressions;

namespace Attrition.API.Services;

public class AdminService : IAdminService
{
    private readonly IUserRepository _userRepo;
    private readonly IWikiRepository _wikiRepo;
    private readonly IForumRepository _forumRepo;
    private readonly IRepository<WikiCategory> _wikiCategoryRepo;
    private readonly IRepository<ForumCategory> _forumCategoryRepo;
    private readonly IRepository<ForumPost> _forumPostRepo;
    private readonly IRepository<ForumReaction> _forumReactionRepo;
    private readonly IRepository<WikiContribution> _wikiContributionRepo;
    private readonly IRepository<MusicAlbum> _musicAlbumRepo;
    private readonly IRepository<MusicTrack> _musicTrackRepo;
    private readonly IRepository<PostReport> _reportRepo;

    public AdminService(
        IUserRepository userRepo,
        IWikiRepository wikiRepo,
        IForumRepository forumRepo,
        IRepository<WikiCategory> wikiCategoryRepo,
        IRepository<ForumCategory> forumCategoryRepo,
        IRepository<ForumPost> forumPostRepo,
        IRepository<ForumReaction> forumReactionRepo,
        IRepository<WikiContribution> wikiContributionRepo,
        IRepository<MusicAlbum> musicAlbumRepo,
        IRepository<MusicTrack> musicTrackRepo,
        IRepository<PostReport> reportRepo)
    {
        _userRepo = userRepo;
        _wikiRepo = wikiRepo;
        _forumRepo = forumRepo;
        _wikiCategoryRepo = wikiCategoryRepo;
        _forumCategoryRepo = forumCategoryRepo;
        _forumPostRepo = forumPostRepo;
        _forumReactionRepo = forumReactionRepo;
        _wikiContributionRepo = wikiContributionRepo;
        _musicAlbumRepo = musicAlbumRepo;
        _musicTrackRepo = musicTrackRepo;
        _reportRepo = reportRepo;
    }


    public async Task<PaginatedResponse<AdminUserDto>> ListUsersAsync(int page, int pageSize, string? search, string sort)
    {
        Expression<Func<User, bool>>? filter = null;

        if (!string.IsNullOrEmpty(search))
        {
            var match = Regex.Match(search.Trim(), @"^(role|email):\s*""?([^""]+)""?", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var tag = match.Groups[1].Value.ToLower();
                var term = match.Groups[2].Value.ToLower();
                if (tag == "role") filter = u => u.Role.ToLower() == term;
                else if (tag == "email") filter = u => u.Email != null && u.Email.ToLower().Contains(term);
            }
            else
            {
                filter = u => u.Username.Contains(search) || (u.Email != null && u.Email.Contains(search));
            }
        }

        Func<IQueryable<User>, IOrderedQueryable<User>> orderBy;
        if (sort == "role")
            orderBy = q => q.OrderBy(u => u.Role == "Admin" ? 0 : 1).ThenByDescending(u => u.JoinedAt);
        else
            orderBy = q => q.OrderByDescending(u => u.JoinedAt);

        var (items, totalCount) = await _userRepo.GetPagedAsync(page, pageSize, filter, orderBy);

        var dtos = items.Select(u => new AdminUserDto(
            u.Id, u.Username, u.Email, u.DisplayName, u.Role,
            u.AvatarPath, u.GoogleAvatarUrl, u.AuthProvider,
            u.IsBanned, u.JoinedAt, u.LastLoginAt,
            u.PostCount, u.ContributionCount
        )).ToList();

        return new PaginatedResponse<AdminUserDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<ApiResponse> DeleteUserAsync(Guid id, Guid adminId)
    {
        if (id == adminId) return new ApiResponse(false, "Cannot delete your own account.");

        var user = await _userRepo.GetByIdAsync(id);
        if (user == null) return new ApiResponse(false, "User not found.");

        // Cascade delete reactions
        var (reactions, _) = await _forumReactionRepo.GetPagedAsync(1, int.MaxValue, r => r.UserId == id);
        foreach (var r in reactions) await _forumReactionRepo.DeleteAsync(r);

        // Cascade delete posts
        var (posts, _) = await _forumPostRepo.GetPagedAsync(1, int.MaxValue, p => p.AuthorId == id);
        foreach (var p in posts) await _forumPostRepo.DeleteAsync(p);

        // Cascade delete threads
        var (threads, _) = await _forumRepo.GetPagedAsync(1, int.MaxValue, t => t.AuthorId == id);
        foreach (var t in threads) await _forumRepo.DeleteAsync(t);

        // Cascade delete contributions
        var (contributions, _) = await _wikiContributionRepo.GetPagedAsync(1, int.MaxValue, c => c.ContributorId == id);
        foreach (var c in contributions) await _wikiContributionRepo.DeleteAsync(c);

        await _userRepo.DeleteAsync(user);
        return new ApiResponse(true);
    }

    public async Task<PaginatedResponse<AdminWikiArticleDto>> ListWikiArticlesAsync(int page, int pageSize, string? search)
    {
        Expression<Func<WikiArticle, bool>>? filter = null;
        if (!string.IsNullOrEmpty(search))
            filter = a => a.Title.Contains(search) || a.Slug.Contains(search);

        var (items, totalCount) = await _wikiRepo.GetPagedAsync(
            page, pageSize, filter,
            q => q.OrderByDescending(a => a.UpdatedAt)
        );

        var categoryIds = items.Select(a => a.CategoryId).Distinct().ToList();
        var authorIds = items.Select(a => a.CreatedById).Distinct().ToList();

        var (categories, _) = await _wikiCategoryRepo.GetPagedAsync(1, int.MaxValue, c => categoryIds.Contains(c.Id));
        var (authors, _) = await _userRepo.GetPagedAsync(1, int.MaxValue, u => authorIds.Contains(u.Id));

        var categoryMap = categories.ToDictionary(c => c.Id, c => c.Name);
        var authorMap = authors.ToDictionary(u => u.Id, u => u.Username);

        var dtos = items.Select(a => new AdminWikiArticleDto(
            a.Id, a.Title, a.Slug,
            categoryMap.GetValueOrDefault(a.CategoryId),
            a.CreatedById.HasValue ? authorMap.GetValueOrDefault(a.CreatedById.Value) : null,
            a.CreatedAt, a.UpdatedAt
        )).ToList();

        return new PaginatedResponse<AdminWikiArticleDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<bool> DeleteWikiArticleAsync(Guid id)
    {
        var article = await _wikiRepo.GetByIdAsync(id);
        if (article == null) return false;

        await _wikiRepo.DeleteAsync(article);
        return true;
    }

    public async Task<PaginatedResponse<AdminWikiCategoryDto>> ListWikiCategoriesAsync(int page, int pageSize, string? search)
    {
        Expression<Func<WikiCategory, bool>>? filter = null;
        if (!string.IsNullOrEmpty(search))
            filter = c => c.Name.Contains(search) || (c.Description != null && c.Description.Contains(search));

        var (items, totalCount) = await _wikiCategoryRepo.GetPagedAsync(
            page, pageSize, filter,
            q => q.OrderBy(c => c.Name)
        );

        var dtos = new List<AdminWikiCategoryDto>();
        foreach (var c in items)
        {
            var articleCount = await _wikiRepo.CountAsync(a => a.CategoryId == c.Id);
            dtos.Add(new AdminWikiCategoryDto(
                c.Id, c.Name, c.Slug, c.Description, c.IconUrl, articleCount
            ));
        }

        return new PaginatedResponse<AdminWikiCategoryDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<WikiCategory> CreateWikiCategoryAsync(WikiCategoryRequest req)
    {
        var category = new WikiCategory
        {
            Name = req.Name,
            Slug = req.Name.ToLower().Replace(" ", "-"),
            Description = req.Description ?? string.Empty,
            IconUrl = req.IconUrl
        };
        return await _wikiCategoryRepo.AddAsync(category);
    }

    public async Task<WikiCategory?> UpdateWikiCategoryAsync(int id, WikiCategoryRequest req)
    {
        var category = await _wikiCategoryRepo.GetByIdAsync(id);
        if (category == null) return null;

        category.Name = req.Name;
        category.Slug = req.Name.ToLower().Replace(" ", "-");
        category.Description = req.Description ?? string.Empty;
        category.IconUrl = req.IconUrl;

        await _wikiCategoryRepo.UpdateAsync(category);
        return category;
    }

    public async Task<(bool Found, bool HasArticles)> DeleteWikiCategoryAsync(int id)
    {
        var category = await _wikiCategoryRepo.GetByIdAsync(id);
        if (category == null) return (false, false);

        var hasArticles = await _wikiRepo.CountAsync(a => a.CategoryId == id) > 0;
        if (hasArticles) return (true, true);

        await _wikiCategoryRepo.DeleteAsync(category);
        return (true, false);
    }

    public async Task<PaginatedResponse<AdminForumThreadDto>> ListForumThreadsAsync(int page, int pageSize, string? search)
    {
        Expression<Func<ForumThread, bool>>? filter = null;
        if (!string.IsNullOrEmpty(search))
            filter = t => t.Title.Contains(search);

        var (items, totalCount) = await _forumRepo.GetPagedAsync(
            page, pageSize, filter,
            q => q.OrderByDescending(t => t.CreatedAt)
        );

        var categoryIds = items.Select(t => t.CategoryId).Distinct().ToList();
        var authorIds = items.Select(t => t.AuthorId).Distinct().ToList();

        var (categories, _) = await _forumCategoryRepo.GetPagedAsync(1, int.MaxValue, c => categoryIds.Contains(c.Id));
        var (authors, _) = await _userRepo.GetPagedAsync(1, int.MaxValue, u => authorIds.Contains(u.Id));

        var categoryMap = categories.ToDictionary(c => c.Id, c => c.Name);
        var authorMap = authors.ToDictionary(u => u.Id, u => u.Username);

        var dtos = items.Select(t => new AdminForumThreadDto(
            t.Id, t.Title, t.IsPinned, t.IsLocked, t.ReplyCount, t.CreatedAt, t.LastReplyAt,
            categoryMap.GetValueOrDefault(t.CategoryId),
            authorMap.GetValueOrDefault(t.AuthorId)
        )).ToList();

        return new PaginatedResponse<AdminForumThreadDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<AdminTogglePinResponse?> TogglePinAsync(Guid id)
    {
        var thread = await _forumRepo.GetByIdAsync(id);
        if (thread == null) return null;

        thread.IsPinned = !thread.IsPinned;
        await _forumRepo.UpdateAsync(thread);
        return new AdminTogglePinResponse(thread.IsPinned);
    }

    public async Task<AdminToggleLockResponse?> ToggleLockAsync(Guid id)
    {
        var thread = await _forumRepo.GetByIdAsync(id);
        if (thread == null) return null;

        thread.IsLocked = !thread.IsLocked;
        await _forumRepo.UpdateAsync(thread);
        return new AdminToggleLockResponse(thread.IsLocked);
    }

    public async Task<bool> DeleteThreadAsync(Guid id)
    {
        var thread = await _forumRepo.GetByIdAsync(id);
        if (thread == null) return false;

        // Delete all posts in thread
        var (posts, _) = await _forumPostRepo.GetPagedAsync(1, int.MaxValue, p => p.ThreadId == id);
        foreach (var post in posts)
        {
            await _forumPostRepo.DeleteAsync(post);
        }

        await _forumRepo.DeleteAsync(thread);
        return true;
    }

    public async Task<PaginatedResponse<AdminForumPostDto>> ListForumPostsAsync(int page, int pageSize, bool? removedOnly, string? search)
    {
        Expression<Func<ForumPost, bool>>? filter = null;
        if (removedOnly == true && !string.IsNullOrEmpty(search))
            filter = p => p.IsRemoved && p.Content.Contains(search);
        else if (removedOnly == true)
            filter = p => p.IsRemoved;
        else if (!string.IsNullOrEmpty(search))
            filter = p => p.Content.Contains(search);

        var (items, totalCount) = await _forumPostRepo.GetPagedAsync(
            page, pageSize, filter,
            q => q.OrderByDescending(p => p.CreatedAt)
        );

        var authorIds = items.Select(p => p.AuthorId).Distinct().ToList();
        var threadIds = items.Select(p => p.ThreadId).Distinct().ToList();
        var removedByIds = items.Where(p => p.RemovedByUserId != null).Select(p => p.RemovedByUserId!.Value).Distinct().ToList();

        var (authors, _) = await _userRepo.GetPagedAsync(1, int.MaxValue, u => authorIds.Contains(u.Id));
        var (threads, _) = await _forumRepo.GetPagedAsync(1, int.MaxValue, t => threadIds.Contains(t.Id));
        var (moderators, _) = await _userRepo.GetPagedAsync(1, int.MaxValue, u => removedByIds.Contains(u.Id));

        var authorMap = authors.ToDictionary(u => u.Id, u => u.Username);
        var threadMap = threads.ToDictionary(t => t.Id, t => t.Title);
        var moderatorMap = moderators.ToDictionary(u => u.Id, u => u.Username);

        var dtos = items.Select(p => new AdminForumPostDto(
            p.Id, p.ThreadId, p.Content, p.CreatedAt, p.UpdatedAt,
            p.IsRemoved, p.RemovedReason, p.RemovedAt,
            authorMap.GetValueOrDefault(p.AuthorId),
            threadMap.GetValueOrDefault(p.ThreadId),
            p.RemovedByUserId.HasValue ? moderatorMap.GetValueOrDefault(p.RemovedByUserId.Value) : null
        )).ToList();

        return new PaginatedResponse<AdminForumPostDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<bool> RemovePostAsync(Guid id, Guid adminId, string reason)
    {
        var post = await _forumPostRepo.GetByIdAsync(id);
        if (post == null) return false;

        post.IsRemoved = true;
        post.RemovedReason = reason;
        post.RemovedByUserId = adminId;
        post.RemovedAt = DateTime.UtcNow;

        await _forumPostRepo.UpdateAsync(post);

        // Mark pending reports as Resolved
        var (reports, _) = await _reportRepo.GetPagedAsync(1, int.MaxValue, r => r.PostId == id && r.Status == "Pending");
        foreach (var report in reports)
        {
            report.Status = "Resolved";
            await _reportRepo.UpdateAsync(report);
        }

        return true;
    }

    public async Task<bool> RestorePostAsync(Guid id)
    {
        var post = await _forumPostRepo.GetByIdAsync(id);
        if (post == null) return false;

        post.IsRemoved = false;
        post.RemovedReason = null;
        post.RemovedByUserId = null;
        post.RemovedAt = null;

        await _forumPostRepo.UpdateAsync(post);
        return true;
    }

    public async Task<AdminStatsDto> GetStatsAsync()
    {
        return new AdminStatsDto(
            TotalUsers: await _userRepo.CountAsync(),
            TotalWikiArticles: await _wikiRepo.CountAsync(),
            TotalForumThreads: await _forumRepo.CountAsync(),
            TotalForumPosts: await _forumPostRepo.CountAsync(),
            PendingContributions: await _wikiContributionRepo.CountAsync(c => c.Status == "Pending"),
            TotalMusicAlbums: await _musicAlbumRepo.CountAsync(),
            TotalMusicTracks: await _musicTrackRepo.CountAsync(),
            RemovedPosts: await _forumPostRepo.CountAsync(p => p.IsRemoved)
        );
    }

    public async Task<PaginatedResponse<AdminPostReportDto>> ListForumReportsAsync(int page, int pageSize, string? status)
    {
        Expression<Func<PostReport, bool>>? filter = null;
        if (!string.IsNullOrEmpty(status))
        {
            filter = r => r.Status == status;
        }

        var (items, totalCount) = await _reportRepo.GetPagedAsync(
            page, pageSize, filter,
            q => q.OrderByDescending(r => r.CreatedAt)
        );

        var dtos = new List<AdminPostReportDto>();
        foreach (var r in items)
        {
            var post = await _forumPostRepo.GetByIdAsync(r.PostId);
            var reporter = await _userRepo.GetByIdAsync(r.ReporterId);
            var author = post != null ? await _userRepo.GetByIdAsync(post.AuthorId) : null;

            dtos.Add(new AdminPostReportDto(
                r.Id,
                r.PostId,
                post?.Content ?? "[Deleted]",
                author?.Username ?? "Unknown",
                reporter?.Username ?? "Unknown",
                r.Reason,
                r.Status,
                r.CreatedAt
            ));
        }

        return new PaginatedResponse<AdminPostReportDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<bool> DismissReportAsync(Guid reportId)
    {
        var report = await _reportRepo.GetByIdAsync(reportId);
        if (report == null) return false;

        report.Status = "Dismissed";
        await _reportRepo.UpdateAsync(report);
        return true;
    }
}

