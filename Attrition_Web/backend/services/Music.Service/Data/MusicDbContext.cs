using Microsoft.EntityFrameworkCore;
using Music.Service.Models;

namespace Music.Service.Data;

public class MusicDbContext : DbContext
{
    public MusicDbContext(DbContextOptions<MusicDbContext> options) : base(options) { }

    public DbSet<MusicAlbum> MusicAlbums => Set<MusicAlbum>();
    public DbSet<MusicTrack> MusicTracks => Set<MusicTrack>();
    public DbSet<MusicPlaylist> MusicPlaylists => Set<MusicPlaylist>();
    public DbSet<PlaylistTrack> PlaylistTracks => Set<PlaylistTrack>();
    public DbSet<UserFavorite> UserFavorites => Set<UserFavorite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("music");

        modelBuilder.Entity<MusicAlbum>(e =>
        {
            e.HasKey(ma => ma.AlbumId);
            e.HasIndex(a => a.Slug).IsUnique();
        });

        modelBuilder.Entity<MusicTrack>(e =>
        {
            e.HasKey(mt => mt.TrackId);
            e.HasIndex(t => new { t.AlbumId, t.TrackNumber }).IsUnique();
            e.HasOne<MusicAlbum>().WithMany(a => a.Tracks).HasForeignKey(t => t.AlbumId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MusicPlaylist>(e =>
        {
            e.HasKey(mp => mp.PlaylistId);
            e.HasIndex(p => p.UserId);
        });

        modelBuilder.Entity<PlaylistTrack>(e =>
        {
            e.HasKey(pt => new { pt.PlaylistId, pt.TrackId });
            e.HasOne<MusicPlaylist>().WithMany(p => p.Tracks).HasForeignKey(pt => pt.PlaylistId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserFavorite>(e =>
        {
            e.HasKey(uf => new { uf.UserId, uf.TrackId });
        });
    }
}
