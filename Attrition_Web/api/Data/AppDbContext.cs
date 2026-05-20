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

    // Game Models
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Gear> Gears => Set<Gear>();
    public DbSet<GearEffect> GearEffects => Set<GearEffect>();
    public DbSet<Consumable> Consumables => Set<Consumable>();
    public DbSet<Enemy> Enemies => Set<Enemy>();
    public DbSet<EnemyLootEntry> EnemyLootTable => Set<EnemyLootEntry>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<SkillEffect> SkillEffects => Set<SkillEffect>();
    public DbSet<Level> Levels => Set<Level>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<GameSave> GameSaves => Set<GameSave>();
    public DbSet<CharacterInventorySlot> CharacterInventory => Set<CharacterInventorySlot>();
    public DbSet<CharacterSkill> CharacterSkills => Set<CharacterSkill>();
    public DbSet<GameRoom> GameRooms => Set<GameRoom>();
    public DbSet<RoomPlayer> RoomPlayers => Set<RoomPlayer>();

    // Music Models
    public DbSet<MusicAlbum> MusicAlbums => Set<MusicAlbum>();
    public DbSet<MusicTrack> MusicTracks => Set<MusicTrack>();
    public DbSet<MusicPlaylist> MusicPlaylists => Set<MusicPlaylist>();
    public DbSet<PlaylistTrack> PlaylistTracks => Set<PlaylistTrack>();
    public DbSet<UserFavorite> UserFavorites => Set<UserFavorite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Email).IsUnique(); // Filtered by EF default if nullable, or we can use HasFilter but standard unique handles nulls differently in Postgres
            e.HasIndex(u => u.GoogleId).IsUnique();
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

        // Game - Items & Equipment (Table-Per-Type)
        modelBuilder.Entity<Item>().ToTable("items");
        modelBuilder.Entity<Gear>().ToTable("gears");
        modelBuilder.Entity<Consumable>().ToTable("consumables");

        modelBuilder.Entity<EnemyLootEntry>(e =>
        {
            e.HasKey(el => new { el.EnemyId, el.ItemId });
        });

        modelBuilder.Entity<GameSave>(e =>
        {
            e.HasKey(gs => gs.SaveId);
            e.HasIndex(gs => new { gs.CharacterId, gs.CreatedAt }).IsDescending(false, true);
        });

        // Level
        modelBuilder.Entity<Level>(e =>
        {
            e.HasKey(l => l.LevelNumber);
        });

        // Character
        modelBuilder.Entity<Character>(e =>
        {
            e.HasIndex(c => new { c.UserId, c.CharacterName }).IsUnique();
        });

        modelBuilder.Entity<CharacterInventorySlot>(e =>
        {
            e.HasKey(ci => new { ci.CharacterId, ci.SlotIndex });
        });

        modelBuilder.Entity<CharacterSkill>(e =>
        {
            e.HasKey(cs => new { cs.CharacterId, cs.SkillId });
        });

        // Multiplayer
        modelBuilder.Entity<GameRoom>(e =>
        {
            e.HasKey(gr => gr.RoomId);
            e.HasIndex(gr => gr.RoomCode).IsUnique();
            e.HasIndex(gr => gr.Status);
        });

        modelBuilder.Entity<RoomPlayer>(e =>
        {
            e.HasKey(rp => new { rp.RoomId, rp.UserId });
        });

        // Music
        modelBuilder.Entity<MusicAlbum>(e =>
        {
            e.HasKey(ma => ma.AlbumId);
            e.HasIndex(a => a.Slug).IsUnique();
        });

        modelBuilder.Entity<MusicTrack>(e =>
        {
            e.HasKey(mt => mt.TrackId);
            e.HasIndex(t => new { t.AlbumId, t.TrackNumber }).IsUnique();
        });

        modelBuilder.Entity<MusicPlaylist>(e =>
        {
            e.HasKey(mp => mp.PlaylistId);
        });

        modelBuilder.Entity<PlaylistTrack>(e =>
        {
            e.HasKey(pt => new { pt.PlaylistId, pt.TrackId });
        });

        modelBuilder.Entity<UserFavorite>(e =>
        {
            e.HasKey(uf => new { uf.UserId, uf.TrackId });
        });
    }
}
