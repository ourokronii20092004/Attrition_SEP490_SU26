using BuildingBlocks.Persistence;
using Music.Service.DTOs;
using Music.Service.Models;

namespace Music.Service.Services;

public class FavoriteService : IFavoriteService
{
    private readonly IRepository<MusicAlbum> _albumRepo;
    private readonly IRepository<MusicTrack> _trackRepo;
    private readonly IRepository<UserFavorite> _favoriteRepo;

    public FavoriteService(IRepository<MusicAlbum> albumRepo, IRepository<MusicTrack> trackRepo, IRepository<UserFavorite> favoriteRepo)
    {
        _albumRepo = albumRepo;
        _trackRepo = trackRepo;
        _favoriteRepo = favoriteRepo;
    }

    public async Task<IEnumerable<FavoriteTrackDto>> GetFavoritesAsync(Guid userId)
    {
        var (favorites, _) = await _favoriteRepo.GetPagedAsync(1, int.MaxValue, f => f.UserId == userId,
            q => q.OrderByDescending(f => f.AddedAt));

        var dtos = new List<FavoriteTrackDto>();
        foreach (var f in favorites)
        {
            var track = await _trackRepo.GetByIdAsync(f.TrackId);
            if (track == null) continue;
            var album = await _albumRepo.GetByIdAsync(track.AlbumId);
            dtos.Add(new FavoriteTrackDto(track.TrackId, track.AlbumId, track.Title, track.Slug, track.Artists,
                track.TrackNumber, track.Duration, track.Genre, track.CoverPath, track.PlayCount,
                album?.Title ?? string.Empty, album?.CoverPath ?? string.Empty, f.AddedAt));
        }
        return dtos;
    }

    public async Task<IEnumerable<int>> GetFavoriteIdsAsync(Guid userId)
    {
        var (favorites, _) = await _favoriteRepo.GetPagedAsync(1, int.MaxValue, f => f.UserId == userId);
        return favorites.Select(f => f.TrackId);
    }

    public async Task<(bool success, bool isFavorited, string? error)> ToggleFavoriteAsync(Guid userId, int trackId)
    {
        var (existingList, _) = await _favoriteRepo.GetPagedAsync(1, 1, f => f.UserId == userId && f.TrackId == trackId);
        var existing = existingList.FirstOrDefault();
        if (existing != null)
        {
            await _favoriteRepo.DeleteAsync(existing);
            return (true, false, null);
        }

        var track = await _trackRepo.GetByIdAsync(trackId);
        if (track == null) return (false, false, "Track not found");

        await _favoriteRepo.AddAsync(new UserFavorite { UserId = userId, TrackId = trackId });
        return (true, true, null);
    }
}

public class PlaylistService : IPlaylistService
{
    private readonly IRepository<MusicPlaylist> _playlistRepo;
    private readonly IRepository<PlaylistTrack> _playlistTrackRepo;

    public PlaylistService(IRepository<MusicPlaylist> playlistRepo, IRepository<PlaylistTrack> playlistTrackRepo)
    {
        _playlistRepo = playlistRepo;
        _playlistTrackRepo = playlistTrackRepo;
    }

    public async Task<IEnumerable<MusicPlaylist>> GetPlaylistsAsync(Guid userId)
    {
        var (playlists, _) = await _playlistRepo.GetPagedAsync(1, int.MaxValue, p => p.UserId == userId);
        return playlists;
    }

    public Task<MusicPlaylist?> GetPlaylistAsync(Guid id) => _playlistRepo.GetByIdAsync(id);

    public async Task<MusicPlaylist> CreatePlaylistAsync(Guid userId, string name, string? description)
    {
        var playlist = new MusicPlaylist { UserId = userId, Title = name, Description = description ?? string.Empty };
        return await _playlistRepo.AddAsync(playlist);
    }

    public async Task<bool> AddTrackToPlaylistAsync(Guid playlistId, int trackId)
    {
        var (existing, _) = await _playlistTrackRepo.GetPagedAsync(1, 1, pt => pt.PlaylistId == playlistId && pt.TrackId == trackId);
        if (existing.Count > 0) return true;
        await _playlistTrackRepo.AddAsync(new PlaylistTrack { PlaylistId = playlistId, TrackId = trackId });
        return true;
    }

    public async Task<bool> RemoveTrackFromPlaylistAsync(Guid playlistId, int trackId)
    {
        var (existing, _) = await _playlistTrackRepo.GetPagedAsync(1, 1, pt => pt.PlaylistId == playlistId && pt.TrackId == trackId);
        var pt = existing.FirstOrDefault();
        if (pt == null) return false;
        await _playlistTrackRepo.DeleteAsync(pt);
        return true;
    }
}
