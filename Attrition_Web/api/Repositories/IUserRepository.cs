using Attrition.API.Models;

namespace Attrition.API.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByGoogleIdAsync(string googleId);
    Task<User?> GetByUsernameAsync(string username);
    Task<bool> IsUsernameAvailableAsync(string username);
}
