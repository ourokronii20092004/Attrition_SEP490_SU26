using Attrition.API.Data;
using Attrition.API.DTOs;
using Attrition.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Attrition.API.Repositories;

public class ForumRepository : Repository<ForumThread>, IForumRepository
{
    public ForumRepository(AppDbContext db) : base(db)
    {
    }

    public async Task<PaginatedResponse<UserPostDto>> GetUserPostsPagedAsync(Guid userId, int page, int pageSize)
    {
        var query = _db.ForumPosts
            .Where(p => p.AuthorId == userId)
            .Join(_db.ForumThreads, p => p.ThreadId, t => t.Id, (p, t) => new UserPostDto(p.Id, p.ThreadId, p.Content, p.CreatedAt, t.Title))
            .OrderByDescending(x => x.CreatedAt);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PaginatedResponse<UserPostDto>(items, total, page, pageSize);
    }
}
