using BuildingBlocks.Persistence;
using Forum.Service.Data;
using Forum.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Forum.Service.Repositories;

public class ForumRepository : Repository<ForumThread>, IForumRepository
{
    private readonly ForumDbContext _context;

    public ForumRepository(ForumDbContext context) : base(context) => _context = context;

    public async Task<List<ForumCategory>> GetCategoriesAsync() =>
        await _context.ForumCategories.OrderBy(c => c.SortOrder).ToListAsync();

    public async Task<ForumCategory?> GetCategoryBySlugAsync(string slug) =>
        await _context.ForumCategories.FirstOrDefaultAsync(c => c.Slug == slug);

    public async Task<ForumCategory?> GetCategoryByIdAsync(int id) =>
        await _context.ForumCategories.FindAsync(id);

    public async Task<List<ForumThread>> SearchThreadsAsync(string query, int limit)
    {
        var s = query.ToLower();
        return await _context.ForumThreads
            .Where(t => t.Title.ToLower().Contains(s))
            .OrderByDescending(t => t.LastReplyAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task CreateThreadWithFirstPostAsync(ForumThread thread, ForumPost firstPost, ThreadSubscription subscription)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        _context.ForumThreads.Add(thread);
        _context.ForumPosts.Add(firstPost);
        _context.ThreadSubscriptions.Add(subscription);
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task DeleteThreadCascadeAsync(Guid threadId)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        var postIds = await _context.ForumPosts.Where(p => p.ThreadId == threadId).Select(p => p.Id).ToListAsync();
        await _context.ForumReactions.Where(r => postIds.Contains(r.PostId)).ExecuteDeleteAsync();
        await _context.PostReports.Where(r => postIds.Contains(r.PostId)).ExecuteDeleteAsync();
        await _context.ForumPosts.Where(p => p.ThreadId == threadId).ExecuteDeleteAsync();
        await _context.ThreadSubscriptions.Where(s => s.ThreadId == threadId).ExecuteDeleteAsync();
        await _context.ForumThreads.Where(t => t.Id == threadId).ExecuteDeleteAsync();
        await tx.CommitAsync();
    }

    public async Task DeletePostCascadeAsync(ForumPost post)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        await _context.ForumReactions.Where(r => r.PostId == post.Id).ExecuteDeleteAsync();
        await _context.PostReports.Where(r => r.PostId == post.Id).ExecuteDeleteAsync();
        await _context.ForumPosts.Where(p => p.Id == post.Id).ExecuteDeleteAsync();
        await _context.ForumThreads
            .Where(t => t.Id == post.ThreadId && t.ReplyCount > 0)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ReplyCount, t => t.ReplyCount - 1));
        await tx.CommitAsync();
    }

    public async Task IncrementReplyCountAsync(Guid threadId, DateTime lastReplyAt)
    {
        await _context.ForumThreads
            .Where(t => t.Id == threadId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.ReplyCount, t => t.ReplyCount + 1)
                .SetProperty(t => t.LastReplyAt, lastReplyAt));
    }

    public async Task<string?> GetFirstPostSnippetAsync(Guid threadId) =>
        await _context.ForumPosts
            .Where(p => p.ThreadId == threadId && !p.IsRemoved)
            .OrderBy(p => p.CreatedAt)
            .Select(p => p.Content)
            .FirstOrDefaultAsync();
}
