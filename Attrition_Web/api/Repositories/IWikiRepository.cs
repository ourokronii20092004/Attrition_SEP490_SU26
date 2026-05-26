using Attrition.API.DTOs;
using Attrition.API.Models;

namespace Attrition.API.Repositories;

public interface IWikiRepository : IRepository<WikiArticle>
{
    Task<PaginatedResponse<UserContributionDto>> GetUserContributionsPagedAsync(Guid userId, int page, int pageSize);
}
