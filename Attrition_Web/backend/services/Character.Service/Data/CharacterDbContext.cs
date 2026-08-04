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
    public DbSet<CharacterSaveEntity> CharacterSaves => Set<CharacterSaveEntity>();
    public DbSet<RoomStateSaveEntity> RoomStateSaves => Set<RoomStateSaveEntity>();

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
            // Fog-of-war is room-level (the party explores one shared map), so it lives here rather
            // than duplicated per character. Array of "scene:cellX:cellY" keys.
            e.Property(x => x.FogJson).HasColumnType("jsonb");
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

            // Owned value objects — columns stay on character_session with explicit names
            // to match the existing schema (zero-migration change).
            e.OwnsOne(x => x.Vitals, b =>
            {
                b.Property(v => v.MaxHp).HasColumnName("MaxHp");
                b.Property(v => v.CurrentHp).HasColumnName("CurrentHp");
                b.Property(v => v.MaxMana).HasColumnName("MaxMana");
                b.Property(v => v.CurrentMana).HasColumnName("CurrentMana");
                b.Property(v => v.MaxStamina).HasColumnName("MaxStamina");
            });
            e.OwnsOne(x => x.Combat, b =>
            {
                b.Property(c => c.AttackSpeed).HasColumnName("AttackSpeed");
                b.Property(c => c.PotionMaxFlasks).HasColumnName("PotionMaxFlasks");
                b.Property(c => c.PotionMaxManaFlasks).HasColumnName("PotionMaxManaFlasks");
                b.Property(c => c.HealthCharges).HasColumnName("HealthCharges");
                b.Property(c => c.ManaCharges).HasColumnName("ManaCharges");
                b.Property(c => c.Ad).HasColumnName("Ad");
                b.Property(c => c.Ap).HasColumnName("Ap");
                b.Property(c => c.Def).HasColumnName("Def");
                b.Property(c => c.Res).HasColumnName("Res");
            });
            e.OwnsOne(x => x.Position, b =>
            {
                b.Property(p => p.PosX).HasColumnName("PosX");
                b.Property(p => p.PosY).HasColumnName("PosY");
                b.Property(p => p.PosZ).HasColumnName("PosZ");
                b.Property(p => p.LastRestPointId).HasColumnName("LastRestPointId").HasMaxLength(50);
            });

            e.HasOne(x => x.Session)
                .WithMany(s => s.Characters)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CharacterSaveEntity>(e =>
        {
            e.ToTable("character_saves");
            e.HasKey(x => x.Id);
            e.Property(x => x.RoomCode).HasMaxLength(32);
            e.Property(x => x.CurrentScene).HasMaxLength(100);
            e.Property(x => x.EventType).HasMaxLength(20);
            e.Property(x => x.AllocatedPointsJson).HasColumnType("jsonb");
            e.Property(x => x.InventoryJson).HasColumnType("jsonb");
            e.Property(x => x.EquipmentJson).HasColumnType("jsonb");

            // Every read is "this character's saves, newest first", and the retention prune needs
            // the oldest — both served by this one descending index.
            e.HasIndex(x => new { x.CharacterId, x.CapturedAt }).IsDescending(false, true);

            // Same owned value objects as character_session, so a save and the live row can be
            // compared field-for-field without a mapping layer in between.
            e.OwnsOne(x => x.Vitals, b =>
            {
                b.Property(v => v.MaxHp).HasColumnName("MaxHp");
                b.Property(v => v.CurrentHp).HasColumnName("CurrentHp");
                b.Property(v => v.MaxMana).HasColumnName("MaxMana");
                b.Property(v => v.CurrentMana).HasColumnName("CurrentMana");
                b.Property(v => v.MaxStamina).HasColumnName("MaxStamina");
            });
            e.OwnsOne(x => x.Combat, b =>
            {
                b.Property(c => c.AttackSpeed).HasColumnName("AttackSpeed");
                b.Property(c => c.PotionMaxFlasks).HasColumnName("PotionMaxFlasks");
                b.Property(c => c.PotionMaxManaFlasks).HasColumnName("PotionMaxManaFlasks");
                b.Property(c => c.HealthCharges).HasColumnName("HealthCharges");
                b.Property(c => c.ManaCharges).HasColumnName("ManaCharges");
                b.Property(c => c.Ad).HasColumnName("Ad");
                b.Property(c => c.Ap).HasColumnName("Ap");
                b.Property(c => c.Def).HasColumnName("Def");
                b.Property(c => c.Res).HasColumnName("Res");
            });
            e.OwnsOne(x => x.Position, b =>
            {
                b.Property(pp => pp.PosX).HasColumnName("PosX");
                b.Property(pp => pp.PosY).HasColumnName("PosY");
                b.Property(pp => pp.PosZ).HasColumnName("PosZ");
                b.Property(pp => pp.LastRestPointId).HasColumnName("LastRestPointId").HasMaxLength(50);
            });
        });

        modelBuilder.Entity<RoomStateSaveEntity>(e =>
        {
            e.ToTable("room_state_saves");
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).HasMaxLength(20);
            e.Property(x => x.CurrentScene).HasMaxLength(100);
            e.Property(x => x.WorldStatesJson).HasColumnType("jsonb");
            e.Property(x => x.FogJson).HasColumnType("jsonb");

            // Read newest-first per room; the retention prune wants the oldest. One index does both.
            e.HasIndex(x => new { x.SessionId, x.CapturedAt }).IsDescending(false, true);

            // Deleting a room takes its state history with it, like every other child row.
            e.HasOne<SessionEntity>()
                .WithMany()
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
