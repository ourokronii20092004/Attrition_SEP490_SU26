using Attrition.API.Data;
using Attrition.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Attrition.API.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext db) : base(db)
    {
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email.ToLower());
    }

    public async Task<User?> GetByGoogleIdAsync(string googleId)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.GoogleId == googleId);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
    }

    public async Task<bool> IsUsernameAvailableAsync(string username)
    {
        return !await _dbSet.AnyAsync(u => u.Username.ToLower() == username.ToLower());
    }
}
