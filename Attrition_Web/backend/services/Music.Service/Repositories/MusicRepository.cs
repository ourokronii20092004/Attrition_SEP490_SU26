using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Music.Service.Data;
using Music.Service.Models;

namespace Music.Service.Repositories;

public class MusicRepository(MusicDbContext context) : IMusicRepository
{
    public IRepository<MusicAlbum> Albums { get; } = new Repository<MusicAlbum>(context);
    public IRepository<MusicTrack> Tracks { get; } = new Repository<MusicTrack>(context);
    public IRepository<UserFavorite> Favorites { get; } = new Repository<UserFavorite>(context);
    public IRepository<MusicPlaylist> Playlists { get; } = new Repository<MusicPlaylist>(context);
    public IRepository<PlaylistTrack> PlaylistTracks { get; } = new Repository<PlaylistTrack>(context);

    public async Task<List<AlbumTrackStats>> GetNewestAlbumStatsAsync(int limit)
    {
        // Project to an anonymous type inside the query — EF can't translate a record-constructor
        // projection after GroupBy — then map to AlbumTrackStats client-side.
        var rows = await context.MusicTracks
            .GroupBy(t => t.AlbumId)
            .Select(g => new { AlbumId = g.Key, TrackCount = g.Count(), NewestTrackAddedAt = g.Max(t => t.CreatedAt) })
            .OrderByDescending(x => x.NewestTrackAddedAt)
            .Take(limit)
            .ToListAsync();
        return rows.Select(x => new AlbumTrackStats(x.AlbumId, x.TrackCount, x.NewestTrackAddedAt)).ToList();
    }

    public async Task<bool> IncrementPlayCountAsync(int trackId, int amount) =>
        await context.MusicTracks.Where(t => t.TrackId == trackId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.PlayCount, t => t.PlayCount + amount)) > 0;
}