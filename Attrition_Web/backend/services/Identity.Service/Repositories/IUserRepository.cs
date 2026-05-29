using BuildingBlocks.Persistence;
using Identity.Service.Models;

namespace Identity.Service.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByGoogleIdAsync(string googleId);
    Task<User?> GetByUsernameAsync(string username);
    Task<bool> IsUsernameAvailableAsync(string username);
    Task<User?> GetByRefreshTokenAsync(string hashedToken);
    Task<User?> GetByPasswordResetTokenAsync(string hashedToken);
    Task<User?> GetByEmailVerificationTokenAsync(string token);
    Task<List<User>> SearchByUsernameAsync(string query, int limit);
}
