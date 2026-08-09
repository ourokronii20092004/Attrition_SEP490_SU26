using Assets.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Assets.Service.Data;

public class AssetsDbContext : DbContext
{
    public AssetsDbContext(DbContextOptions<AssetsDbContext> options) : base(options)
    {
    }

    public DbSet<Asset> Assets => Set<Asset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("assets");

        modelBuilder.Entity<Asset>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.AssetType);
            e.HasIndex(a => new { a.SourceType, a.SourceId })
                .IsUnique()
                .HasFilter("\"SourceType\" IN ('unity-item', 'unity-skill', 'unity-enemy') AND \"SourceId\" IS NOT NULL");
            e.Property(a => a.ContentHash).HasMaxLength(64);
            // No HasOne<User>() — uploader ref is plain Guid + denormalized name.
        });
    }
}