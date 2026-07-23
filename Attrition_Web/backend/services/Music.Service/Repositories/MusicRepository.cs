using BuildingBlocks.Persistence;
using Music.Service.Data;
using Music.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Music.Service.Repositories;

public class MusicRepository(MusicDbContext context) : IMusicRepository
{
    public IRepository<MusicAlbum> Albums { get; } = new Repository<MusicAlbum>(context);
    public IRepository<MusicTrack> Tracks { get; } = new Repository<MusicTrack>(context);
    public IRepository<UserFavorite> Favorites { get; } = new Repository<UserFavorite>(context);
    public IRepository<MusicPlaylist> Playlists { get; } = new Repository<MusicPlaylist>(context);
    public IRepository<PlaylistTrack> PlaylistTracks { get; } = new Repository<PlaylistTrack>(context);

    public Task<List<AlbumTrackStats>> GetNewestAlbumStatsAsync(int limit) => context.MusicTracks
        .GroupBy(t => t.AlbumId)
        .Select(g => new AlbumTrackStats(g.Key, g.Count(), g.Max(t => t.CreatedAt)))
        .OrderByDescending(x => x.NewestTrackAddedAt)
        .Take(limit)
        .ToListAsync();

    public async Task<bool> IncrementPlayCountAsync(int trackId, int amount) =>
        await context.MusicTracks.Where(t => t.TrackId == trackId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.PlayCount, t => t.PlayCount + amount)) > 0;
}
