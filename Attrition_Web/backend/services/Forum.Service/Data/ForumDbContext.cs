using Forum.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Forum.Service.Data;

public class ForumDbContext : DbContext
{
    public ForumDbContext(DbContextOptions<ForumDbContext> options) : base(options) { }

    public DbSet<ForumCategory> ForumCategories => Set<ForumCategory>();
    public DbSet<ForumThread> ForumThreads => Set<ForumThread>();
    public DbSet<ForumPost> ForumPosts => Set<ForumPost>();
    public DbSet<ForumReaction> ForumReactions => Set<ForumReaction>();
    public DbSet<ThreadSubscription> ThreadSubscriptions => Set<ThreadSubscription>();
    public DbSet<PostReport> PostReports => Set<PostReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("forum");

        modelBuilder.Entity<ForumCategory>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.Slug).IsUnique();
        });

        modelBuilder.Entity<ForumThread>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.CategoryId);
            e.Property(t => t.IsPinned).HasDefaultValue(false);
            e.Property(t => t.IsLocked).HasDefaultValue(false);
        });

        modelBuilder.Entity<ForumPost>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.ThreadId);
        });

        modelBuilder.Entity<ForumReaction>(e =>
        {
            e.HasKey(r => r.Id);
            // One reaction per user per post (like OR dislike, never both).
            e.HasIndex(r => new { r.PostId, r.UserId }).IsUnique();
        });

        modelBuilder.Entity<ThreadSubscription>(e =>
        {
            e.HasKey(ts => ts.Id);
            e.HasIndex(ts => new { ts.ThreadId, ts.UserId }).IsUnique();
        });

        modelBuilder.Entity<PostReport>(e =>
        {
            e.HasKey(pr => pr.Id);
            e.HasIndex(pr => pr.Status);
            e.Property(pr => pr.Status).HasDefaultValue(ReportStatus.Pending);
        });
    }
}
