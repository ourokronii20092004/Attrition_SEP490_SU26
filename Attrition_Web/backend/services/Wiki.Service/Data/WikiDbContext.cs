using Microsoft.EntityFrameworkCore;
using Wiki.Service.Models;

namespace Wiki.Service.Data;

public class WikiDbContext : DbContext
{
    public WikiDbContext(DbContextOptions<WikiDbContext> options) : base(options) { }

    public DbSet<WikiCategory> WikiCategories => Set<WikiCategory>();
    public DbSet<WikiArticle> WikiArticles => Set<WikiArticle>();
    public DbSet<WikiRevision> WikiRevisions => Set<WikiRevision>();
    public DbSet<WikiContribution> WikiContributions => Set<WikiContribution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("wiki");

        modelBuilder.Entity<WikiCategory>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.Slug).IsUnique();
        });

        modelBuilder.Entity<WikiArticle>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.Slug).IsUnique();
            e.HasIndex(a => a.CategoryId);
            // No HasOne<User>() — author refs are plain Guid + denormalized name.
        });

        modelBuilder.Entity<WikiRevision>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.ArticleId);
        });

        modelBuilder.Entity<WikiContribution>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.ArticleId);
            e.HasIndex(c => c.Status);
            e.Property(c => c.Status).HasDefaultValue("Pending");
        });
    }
}
