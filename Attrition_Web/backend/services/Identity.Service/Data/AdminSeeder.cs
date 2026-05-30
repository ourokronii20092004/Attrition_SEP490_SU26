using Identity.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Identity.Service.Data;

/// <summary>
/// Ensures a single Admin account exists on startup, driven by Seed:Admin:* config
/// (Username/Password/Email). No-ops when the username is unset or already present,
/// so it is safe to run on every boot.
/// </summary>
public static class AdminSeeder
{
    public static async Task SeedAdminAsync(IdentityDbContext db, IConfiguration config, ILogger logger)
    {
        var username = config["Seed:Admin:Username"];
        var password = config["Seed:Admin:Password"];
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return;

        var email = config["Seed:Admin:Email"];

        if (await db.Users.AnyAsync(u => u.Username == username))
        {
            logger.LogInformation("Admin seed: user '{Username}' already exists, skipping.", username);
            return;
        }

        db.Users.Add(new User
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = "Admin",
            AuthProvider = "local",
            IsEmailVerified = true,
        });
        await db.SaveChangesAsync();
        logger.LogInformation("Admin seed: created admin user '{Username}'.", username);
    }
}
