using BuildingBlocks.Persistence;
using Music.Service.Models;

namespace Music.Service.Repositories.Interface;

public record AlbumTrackStats(int AlbumId, int TrackCount, DateTime NewestTrackAddedAt);

public interface IMusicRepository
{
    IRepository<MusicAlbum> Albums { get; }
    IRepository<MusicTrack> Tracks { get; }
    IRepository<UserFavorite> Favorites { get; }
    IRepository<MusicPlaylist> Playlists { get; }
    IRepository<PlaylistTrack> PlaylistTracks { get; }
    Task<List<AlbumTrackStats>> GetNewestAlbumStatsAsync(int limit);
    Task<bool> IncrementPlayCountAsync(int trackId, int amount);
}
