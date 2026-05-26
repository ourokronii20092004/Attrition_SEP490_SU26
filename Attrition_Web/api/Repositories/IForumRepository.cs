using Attrition.API.DTOs;
using Attrition.API.Models;

namespace Attrition.API.Repositories;

public interface IForumRepository : IRepository<ForumThread>
{
    Task<PaginatedResponse<UserPostDto>> GetUserPostsPagedAsync(Guid userId, int page, int pageSize);
}
