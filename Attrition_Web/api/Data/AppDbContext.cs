using Microsoft.EntityFrameworkCore;
using Attrition.API.Models;

namespace Attrition.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<WikiCategory> WikiCategories => Set<WikiCategory>();
    public DbSet<WikiArticle> WikiArticles => Set<WikiArticle>();
    public DbSet<WikiRevision> WikiRevisions => Set<WikiRevision>();
    public DbSet<WikiContribution> WikiContributions => Set<WikiContribution>();
    public DbSet<ForumCategory> ForumCategories => Set<ForumCategory>();
    public DbSet<ForumThread> ForumThreads => Set<ForumThread>();
    public DbSet<ForumPost> ForumPosts => Set<ForumPost>();
    public DbSet<ForumReaction> ForumReactions => Set<ForumReaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.Role).HasDefaultValue("User");
            e.Property(u => u.JoinedAt).HasDefaultValueSql("NOW()");
            e.Property(u => u.IsBanned).HasDefaultValue(false);
            e.Property(u => u.MustChangePassword).HasDefaultValue(false);
        });

        // Wiki
        modelBuilder.Entity<WikiCategory>(e =>
        {
            e.HasIndex(c => c.Slug).IsUnique();
        });

        modelBuilder.Entity<WikiArticle>(e =>
        {
            e.HasIndex(a => a.Slug).IsUnique();
            e.HasOne<WikiCategory>().WithMany().HasForeignKey(a => a.CategoryId);
            e.HasOne<User>().WithMany().HasForeignKey(a => a.CreatedById).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<User>().WithMany().HasForeignKey(a => a.LastEditedById).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WikiRevision>(e =>
        {
            e.HasOne<WikiArticle>().WithMany().HasForeignKey(r => r.ArticleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(r => r.EditedById).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WikiContribution>(e =>
        {
            e.HasOne<WikiArticle>().WithMany().HasForeignKey(c => c.ArticleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(c => c.ContributorId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(c => c.ReviewedById).OnDelete(DeleteBehavior.SetNull);
            e.Property(c => c.Status).HasDefaultValue("Pending");
        });

        // Forum
        modelBuilder.Entity<ForumCategory>(e =>
        {
            e.HasIndex(c => c.Slug).IsUnique();
        });

        modelBuilder.Entity<ForumThread>(e =>
        {
            e.HasOne<ForumCategory>().WithMany().HasForeignKey(t => t.CategoryId);
            e.HasOne<User>().WithMany().HasForeignKey(t => t.AuthorId).OnDelete(DeleteBehavior.Cascade);
            e.Property(t => t.IsPinned).HasDefaultValue(false);
            e.Property(t => t.IsLocked).HasDefaultValue(false);
        });

        modelBuilder.Entity<ForumPost>(e =>
        {
            e.HasOne<ForumThread>().WithMany().HasForeignKey(p => p.ThreadId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(p => p.AuthorId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ForumReaction>(e =>
        {
            e.HasOne<ForumPost>().WithMany().HasForeignKey(r => r.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => new { r.PostId, r.UserId, r.ReactionType }).IsUnique();
        });
    }
}