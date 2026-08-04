using Forum.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Forum.Service.Data;

public class ForumDbContext(DbContextOptions<ForumDbContext> options) : DbContext(options)
{
    public DbSet<ForumCategory> ForumCategories => Set<ForumCategory>();
    public DbSet<ForumPost> ForumPosts => Set<ForumPost>();
    public DbSet<ForumReaction> ForumReactions => Set<ForumReaction>();
    public DbSet<ThreadSubscription> ThreadSubscriptions => Set<ThreadSubscription>();
    public DbSet<PostReport> PostReports => Set<PostReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("forum");
        modelBuilder.Entity<ForumCategory>(e => { e.HasKey(c => c.Id); e.HasIndex(c => c.Slug).IsUnique(); });
        modelBuilder.Entity<ForumPost>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.RootPostId);
            e.HasIndex(p => p.ParentPostId);
            e.HasIndex(p => p.CategoryId);
            e.HasIndex(p => p.WikiArticleId).IsUnique().HasFilter("\"WikiArticleId\" IS NOT NULL");
            e.Property(p => p.IsPinned).HasDefaultValue(false);
            e.Property(p => p.IsLocked).HasDefaultValue(false);
            e.OwnsOne(p => p.Moderation, b =>
            {
                b.Property(m => m.IsRemoved).HasColumnName("IsRemoved");
                b.Property(m => m.Reason).HasColumnName("RemovedReason");
                b.Property(m => m.ByUserId).HasColumnName("RemovedByUserId");
                b.Property(m => m.ByName).HasColumnName("RemovedByName");
                b.Property(m => m.At).HasColumnName("RemovedAt");
            });
        });
        modelBuilder.Entity<ForumReaction>(e => { e.HasKey(r => r.Id); e.HasIndex(r => new { r.PostId, r.UserId }).IsUnique(); });
        modelBuilder.Entity<ThreadSubscription>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.ThreadId, s.UserId }).IsUnique();
            e.Property(s => s.IsMuted).HasDefaultValue(false);
        });
        modelBuilder.Entity<PostReport>(e => { e.HasKey(r => r.Id); e.HasIndex(r => r.Status); e.Property(r => r.Status).HasDefaultValue(ReportStatus.Pending); });
    }
}
