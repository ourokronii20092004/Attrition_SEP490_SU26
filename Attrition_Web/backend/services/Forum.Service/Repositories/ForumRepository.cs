using BuildingBlocks.Persistence;
using Forum.Service.Data;
using Forum.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Forum.Service.Repositories;

public class ForumRepository : Repository<ForumPost>, IForumRepository
{
    private readonly ForumDbContext context;

    public ForumRepository(ForumDbContext context) : base(context)
    {
        this.context = context;
        Categories = new Repository<ForumCategory>(context);
        Posts = this;
        Reactions = new Repository<ForumReaction>(context);
        Subscriptions = new Repository<ThreadSubscription>(context);
        Reports = new Repository<PostReport>(context);
    }

    public IRepository<ForumCategory> Categories { get; }
    public IRepository<ForumPost> Posts { get; }
    public IRepository<ForumReaction> Reactions { get; }
    public IRepository<ThreadSubscription> Subscriptions { get; }
    public IRepository<PostReport> Reports { get; }

    public Task<List<ForumCategory>> GetCategoriesAsync() => context.ForumCategories.OrderBy(c => c.SortOrder).ToListAsync();

    public async Task<Dictionary<int, (int ThreadCount, DateTime? LatestActivity)>> GetCategoryStatsAsync()
    {
        var rows = await context.ForumPosts.Where(p => p.RootPostId == null && !p.Moderation.IsRemoved && p.CategoryId != null)
            .GroupBy(p => p.CategoryId!.Value).Select(g => new { CategoryId = g.Key, ThreadCount = g.Count(), LatestActivity = (DateTime?)g.Max(p => p.LastReplyAt) }).ToListAsync();
        return rows.ToDictionary(r => r.CategoryId, r => (r.ThreadCount, r.LatestActivity));
    }

    public Task<ForumCategory?> GetCategoryBySlugAsync(string slug) => context.ForumCategories.FirstOrDefaultAsync(c => c.Slug == slug);

    public async Task<ForumCategory?> GetCategoryByIdAsync(int id) => await context.ForumCategories.FindAsync(id);

    public Task<List<ForumPost>> SearchThreadsAsync(string query, int limit)
    {
        var s = query.ToLower();
        return context.ForumPosts.Where(p => p.RootPostId == null && !p.Moderation.IsRemoved && p.WikiArticleId == null && p.Title != null && p.Title.ToLower().Contains(s))
            .OrderByDescending(p => p.LastReplyAt).Take(limit).ToListAsync();
    }

    public Task<ForumPost?> GetByWikiArticleIdAsync(Guid articleId) => context.ForumPosts.FirstOrDefaultAsync(p => p.RootPostId == null && p.WikiArticleId == articleId);

    public async Task CreateRootAsync(ForumPost root, ThreadSubscription? subscription)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () => { await using var tx = await context.Database.BeginTransactionAsync(); context.ForumPosts.Add(root); if (subscription != null) context.ThreadSubscriptions.Add(subscription); await context.SaveChangesAsync(); await tx.CommitAsync(); });
    }

    public async Task DeleteThreadCascadeAsync(Guid rootId)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await context.Database.BeginTransactionAsync();
            var ids = await context.ForumPosts.Where(p => p.Id == rootId || p.RootPostId == rootId).Select(p => p.Id).ToListAsync();
            await context.ForumReactions.Where(r => ids.Contains(r.PostId)).ExecuteDeleteAsync();
            await context.PostReports.Where(r => ids.Contains(r.PostId)).ExecuteDeleteAsync();
            await context.ThreadSubscriptions.Where(s => s.ThreadId == rootId).ExecuteDeleteAsync();
            await context.ForumPosts.Where(p => ids.Contains(p.Id)).ExecuteDeleteAsync();
            await tx.CommitAsync();
        });
    }

    public async Task DeletePostCascadeAsync(ForumPost post)
    {
        if (post.RootPostId == null) { await DeleteThreadCascadeAsync(post.Id); return; }
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await context.Database.BeginTransactionAsync();
            await context.ForumPosts.Where(p => p.ParentPostId == post.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.ParentPostId, post.ParentPostId));
            await context.ForumReactions.Where(r => r.PostId == post.Id).ExecuteDeleteAsync();
            await context.PostReports.Where(r => r.PostId == post.Id).ExecuteDeleteAsync();
            await context.ForumPosts.Where(p => p.Id == post.Id).ExecuteDeleteAsync();
            await context.ForumPosts.Where(p => p.Id == post.RootPostId && p.ReplyCount > 0).ExecuteUpdateAsync(s => s.SetProperty(p => p.ReplyCount, p => p.ReplyCount - 1));
            await tx.CommitAsync();
        });
    }

    public Task IncrementReplyCountAsync(Guid rootId, DateTime at) => context.ForumPosts.Where(p => p.Id == rootId && p.RootPostId == null)
        .ExecuteUpdateAsync(s => s.SetProperty(p => p.ReplyCount, p => p.ReplyCount + 1).SetProperty(p => p.LastReplyAt, at));

    public async Task<(Guid PostId, string Content)?> GetFirstPostAsync(Guid rootId)
    {
        var p = await context.ForumPosts.Where(p => p.Id == rootId && p.RootPostId == null && !p.Moderation.IsRemoved).Select(p => new { p.Id, p.Content }).FirstOrDefaultAsync();
        return p == null ? null : (p.Id, p.Content);
    }

    public async Task<(List<UserReplyRow> Items, int Total)> GetUserRepliesAsync(Guid userId, int page, int pageSize)
    {
        var query = from p in context.ForumPosts
                    join root in context.ForumPosts on p.RootPostId equals root.Id
                    where p.AuthorId == userId && !p.Moderation.IsRemoved && root.WikiArticleId == null
                    orderby p.CreatedAt descending
                    select new UserReplyRow(p.Id, root.Id, root.Title ?? "Discussion", p.Content, p.CreatedAt);
        var total = await query.CountAsync();
        return (await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(), total);
    }
}