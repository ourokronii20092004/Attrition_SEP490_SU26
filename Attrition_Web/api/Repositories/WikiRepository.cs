using Attrition.API.Data;
using Attrition.API.DTOs;
using Attrition.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Attrition.API.Repositories;

public class WikiRepository : Repository<WikiArticle>, IWikiRepository
{
    public WikiRepository(AppDbContext db) : base(db)
    {
    }

    public async Task<PaginatedResponse<UserContributionDto>> GetUserContributionsPagedAsync(Guid userId, int page, int pageSize)
    {
        var query = _db.WikiContributions
            .Where(c => c.ContributorId == userId)
            .Join(_db.WikiArticles, c => c.ArticleId, a => a.Id, (c, a) => new UserContributionDto(c.Id, c.ArticleId, c.ChangeNote, c.SubmittedAt, c.Status, a.Title, a.Slug))
            .OrderByDescending(x => x.CreatedAt);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PaginatedResponse<UserContributionDto>(items, total, page, pageSize);
    }
}
