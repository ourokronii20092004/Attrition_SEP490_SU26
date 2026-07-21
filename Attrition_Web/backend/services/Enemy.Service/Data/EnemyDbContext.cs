using Enemy.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Enemy.Service.Data;

public class EnemyDbContext : DbContext
{
    public EnemyDbContext(DbContextOptions<EnemyDbContext> options) : base(options) { }

    public DbSet<EnemyEntity> Enemies => Set<EnemyEntity>();
    public DbSet<ItemEntity> Items => Set<ItemEntity>();
    public DbSet<SkillEntity> Skills => Set<SkillEntity>();

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

        modelBuilder.Entity<SkillEntity>(e =>
        {
            e.ToTable("skills");
            e.HasKey(x => x.SkillId);
            e.Property(x => x.SkillId).HasMaxLength(64);
            e.Property(x => x.Element).HasMaxLength(30);
            e.Property(x => x.DamageType).HasMaxLength(30);
            e.Property(x => x.Delivery).HasMaxLength(30);
            e.Property(x => x.HitShape).HasMaxLength(30);
        });

        modelBuilder.Entity<ItemEntity>(e =>
        {
            e.ToTable("items");
            e.HasKey(x => x.ItemId);
            e.Property(x => x.ItemId).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Category).HasMaxLength(30);
            e.Property(x => x.Rarity).HasMaxLength(50);
            e.Property(x => x.IconKey).HasMaxLength(100);
            e.HasIndex(x => x.Category);

            // Owned collection → enemy.item_modifiers, shadow FK to item.
            e.OwnsMany(x => x.Modifiers, b =>
            {
                b.ToTable("item_modifiers");
                b.WithOwner().HasForeignKey("ItemId");
                b.Property<int>("Id");
                b.HasKey("Id");
                b.Property(m => m.Stat).HasMaxLength(30);
            });
        });
    }
}
