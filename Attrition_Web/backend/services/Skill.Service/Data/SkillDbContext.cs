using Microsoft.EntityFrameworkCore;
using Skill.Service.Models;

namespace Skill.Service.Data;

public class SkillDbContext(DbContextOptions<SkillDbContext> options) : DbContext(options)
{
    public DbSet<SkillEntity> Skills => Set<SkillEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("skill");
        modelBuilder.Entity<SkillEntity>(e =>
        {
            e.ToTable("skills");
            e.HasKey(x => x.SkillId);
            e.Property(x => x.SkillId).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.IconKey).HasMaxLength(100);
            e.Property(x => x.Rarity).HasMaxLength(50);
            e.Property(x => x.Element).HasMaxLength(30);
            e.Property(x => x.DamageType).HasMaxLength(30);
            e.Property(x => x.Delivery).HasMaxLength(30);
            e.Property(x => x.HitShape).HasMaxLength(30);
            e.HasIndex(x => x.Element);
        });
    }
}
