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
            e.Property(u => u.IsEmailVerified).HasDefaultValue(false);
            e.Property(u => u.NotifyOnReply).HasDefaultValue(true);
            e.Property(u => u.NotifyOnMention).HasDefaultValue(true);
            // Privacy defaults open so the migration doesn't silently hide existing profiles.
            e.Property(u => u.ShowBio).HasDefaultValue(true);
            e.Property(u => u.ShowActivity).HasDefaultValue(true);

            // Owned value objects — columns stay on the Users table with explicit names
            // to match the existing schema (zero-migration change).
            e.OwnsOne(u => u.Refresh, b =>
            {
                b.Property(t => t.Token).HasColumnName("RefreshToken");
                b.Property(t => t.ExpiresAt).HasColumnName("RefreshTokenExpiresAt");
            });
            e.OwnsOne(u => u.EmailVerification, b =>
            {
                b.Property(t => t.Token).HasColumnName("EmailVerificationToken");
                b.Property(t => t.ExpiresAt).HasColumnName("EmailVerificationTokenExpiry");
                b.HasIndex(t => t.Token).HasFilter("\"EmailVerificationToken\" IS NOT NULL");
            });
            e.OwnsOne(u => u.PasswordReset, b =>
            {
                b.Property(t => t.Token).HasColumnName("PasswordResetToken");
                b.Property(t => t.ExpiresAt).HasColumnName("PasswordResetTokenExpiry");
                b.HasIndex(t => t.Token).HasFilter("\"PasswordResetToken\" IS NOT NULL");
            });
            e.OwnsOne(u => u.DeletionConfirm, b =>
            {
                b.Property(t => t.Token).HasColumnName("DeletionConfirmToken");
                b.Property(t => t.ExpiresAt).HasColumnName("DeletionConfirmTokenExpiry");
            });
            e.OwnsOne(u => u.Security, b =>
            {
                b.Property(s => s.LastLoginAt).HasColumnName("LastLoginAt");
                b.Property(s => s.LastLoginIp).HasColumnName("LastLoginIp");
                b.Property(s => s.FailedLoginAttempts).HasColumnName("FailedLoginAttempts").HasDefaultValue(0);
                b.Property(s => s.LockoutEnd).HasColumnName("LockoutEnd");
                b.Property(s => s.TokensValidAfter).HasColumnName("TokensValidAfter");
            });
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
