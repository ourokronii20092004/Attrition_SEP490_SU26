using Character.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Character.Service.Data;

public class CharacterDbContext : DbContext
{
    public CharacterDbContext(DbContextOptions<CharacterDbContext> options) : base(options) { }

    public DbSet<CharacterEntity> Characters => Set<CharacterEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("character");

        modelBuilder.Entity<CharacterEntity>(e =>
        {
            e.ToTable("characters");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Archetype).HasMaxLength(50);
            e.HasIndex(x => x.OwnerId);

            // Owned timeline → character.character_snapshots, shadow FK to character only.
            e.OwnsMany(x => x.Snapshots, b =>
            {
                b.ToTable("character_snapshots");
                b.WithOwner().HasForeignKey("CharacterId");
                b.Property<int>("Id");
                b.HasKey("Id");
                b.Property(s => s.RoomCode).HasMaxLength(32);
                b.Property(s => s.EventType).HasMaxLength(20);
                b.HasIndex("CharacterId", nameof(CharacterSnapshot.CapturedAt));
            });
        });
    }
}
