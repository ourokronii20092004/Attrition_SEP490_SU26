using BuildingBlocks.Persistence;
using Identity.Service.Data;
using Identity.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Identity.Service.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    private readonly IdentityDbContext _context;

    public UserRepository(IdentityDbContext context) : base(context) => _context = context;

    public async Task<User?> GetByEmailAsync(string email) =>
        await _context.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email.ToLower());

    public async Task<User?> GetByGoogleIdAsync(string googleId) =>
        await _context.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId);

    public async Task<User?> GetByUsernameAsync(string username) =>
        await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

    public async Task<bool> IsUsernameAvailableAsync(string username) =>
        !await _context.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower());

    public async Task<User?> GetByRefreshTokenAsync(string hashedToken) =>
        await _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == hashedToken);

    public async Task<User?> GetByPasswordResetTokenAsync(string hashedToken) =>
        await _context.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == hashedToken);

    public async Task<User?> GetByEmailVerificationTokenAsync(string hashedToken) =>
        await _context.Users.FirstOrDefaultAsync(u => u.EmailVerificationToken == hashedToken);

    public async Task<List<User>> SearchByUsernameAsync(string query, int limit) =>
        await _context.Users
            .Where(u => !u.IsBanned && (u.Username.ToLower().Contains(query.ToLower())
                || (u.DisplayName != null && u.DisplayName.ToLower().Contains(query.ToLower()))))
            .OrderBy(u => u.Username)
            .Take(limit)
            .ToListAsync();
}
