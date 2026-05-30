using Enemy.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Enemy.Service.Data;

public class EnemyDbContext : DbContext
{
    public EnemyDbContext(DbContextOptions<EnemyDbContext> options) : base(options) { }

    public DbSet<EnemyEntity> Enemies => Set<EnemyEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("enemy");

        modelBuilder.Entity<EnemyEntity>(e =>
        {
            e.ToTable("enemies");
            e.HasKey(x => x.EnemyId);
            e.Property(x => x.EnemyId).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Tier).HasMaxLength(50);
            e.HasIndex(x => x.Tier);
            e.Property(x => x.AttackSpeed).HasDefaultValue(1.0f);

            // Owned collection → enemy.enemy_loot, shadow FK to enemy only, no items catalog.
            e.OwnsMany(x => x.LootTable, b =>
            {
                b.ToTable("enemy_loot");
                b.WithOwner().HasForeignKey("EnemyId");
                b.Property<int>("Id");
                b.HasKey("Id");
                b.Property(l => l.ItemName).HasMaxLength(100);
                b.Property(l => l.Rarity).HasMaxLength(50);
            });
        });
    }
}
