using Character.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Character.Service.Data;

public class CharacterDbContext : DbContext
{
    public CharacterDbContext(DbContextOptions<CharacterDbContext> options) : base(options) { }

    public DbSet<CharacterEntity> Characters => Set<CharacterEntity>();
    public DbSet<SessionEntity> Sessions => Set<SessionEntity>();
    public DbSet<CharacterSessionEntity> CharacterSessions => Set<CharacterSessionEntity>();
    public DbSet<WorldStateEntity> WorldStates => Set<WorldStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("character");

        modelBuilder.Entity<CharacterEntity>(e =>
        {
            e.ToTable("characters");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Archetype).HasMaxLength(50);
            // Inventory/equipment lưu nguyên khối JSON (Postgres jsonb).
            e.Property(x => x.InventoryJson).HasColumnType("jsonb");
            e.Property(x => x.EquipmentJson).HasColumnType("jsonb");
            e.Property(x => x.QuestsJson).HasColumnType("jsonb");
            e.HasIndex(x => x.OwnerId);
            // One character per (owner, name): the snapshot-ingest resolve-or-create races without
            // this, silently creating duplicates. Service handles the resulting unique violation.
            e.HasIndex(x => new { x.OwnerId, x.Name }).IsUnique();

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

        // Persistent co-op room. Fixed RoomCode (unique) so the host can re-open + re-invite.
        modelBuilder.Entity<SessionEntity>(e =>
        {
            e.ToTable("sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.RoomCode).HasMaxLength(8);
            e.Property(x => x.Name).HasMaxLength(60);
            e.Property(x => x.CurrentScene).HasMaxLength(100);
            e.HasIndex(x => x.OwnerId);
            e.HasIndex(x => x.RoomCode).IsUnique();
        });

        // One character's progress within one room. Composite PK (CharacterId, SessionId).
        modelBuilder.Entity<CharacterSessionEntity>(e =>
        {
            e.ToTable("character_session");
            e.HasKey(x => new { x.CharacterId, x.SessionId });
            e.Property(x => x.AllocatedPointsJson).HasColumnType("jsonb");
            e.Property(x => x.InventoryJson).HasColumnType("jsonb");
            e.Property(x => x.EquipmentJson).HasColumnType("jsonb");
            e.Property(x => x.LastRestPointId).HasMaxLength(50);
            e.HasOne(x => x.Session)
                .WithMany(s => s.Characters)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Quest/world-event progress for a room (host-authoritative). Composite PK (SessionId, EventId).
        modelBuilder.Entity<WorldStateEntity>(e =>
        {
            e.ToTable("world_state");
            e.HasKey(x => new { x.SessionId, x.EventId });
            e.Property(x => x.EventId).HasMaxLength(50);
            e.HasOne(x => x.Session)
                .WithMany(s => s.WorldStates)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
