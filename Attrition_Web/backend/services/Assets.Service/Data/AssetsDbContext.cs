using Assets.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Assets.Service.Data;

public class AssetsDbContext : DbContext
{
    public AssetsDbContext(DbContextOptions<AssetsDbContext> options) : base(options) { }

    public DbSet<Asset> Assets => Set<Asset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("assets");

        modelBuilder.Entity<Asset>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.AssetType);
            // No HasOne<User>() — uploader ref is plain Guid + denormalized name.
        });
    }
}
