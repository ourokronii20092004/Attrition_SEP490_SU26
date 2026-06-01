using Identity.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Identity.Service.Data;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<UserReport> UserReports => Set<UserReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.GoogleId).IsUnique();
            e.Property(u => u.Role).HasDefaultValue("User");
            e.Property(u => u.JoinedAt).HasDefaultValueSql("NOW()");
            e.Property(u => u.IsBanned).HasDefaultValue(false);
            e.Property(u => u.MustChangePassword).HasDefaultValue(false);
            e.Property(u => u.FailedLoginAttempts).HasDefaultValue(0);
            e.Property(u => u.IsEmailVerified).HasDefaultValue(false);
            e.Property(u => u.NotifyOnReply).HasDefaultValue(true);
            e.Property(u => u.NotifyOnMention).HasDefaultValue(true);
            e.HasIndex(u => u.PasswordResetToken).HasFilter("\"PasswordResetToken\" IS NOT NULL");
            e.HasIndex(u => u.EmailVerificationToken).HasFilter("\"EmailVerificationToken\" IS NOT NULL");
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.HasKey(n => n.Id);
            // Drives the hot query: a user's notifications newest-first, and the unread count.
            e.HasIndex(n => new { n.UserId, n.CreatedAt });
            e.HasIndex(n => new { n.UserId, n.IsRead });
        });

        modelBuilder.Entity<UserReport>(e =>
        {
            e.HasKey(r => r.Id);
            // Admin queue filters by status, newest-first.
            e.HasIndex(r => new { r.Status, r.CreatedAt });
            e.HasIndex(r => r.ReportedUserId);
        });
    }
}
