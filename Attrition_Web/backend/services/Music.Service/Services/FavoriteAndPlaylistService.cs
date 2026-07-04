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
        var favorites = await _favoriteRepo.ListAsync(f => f.UserId == userId,
            q => q.OrderByDescending(f => f.AddedAt));

        var trackIds = favorites.Select(f => f.TrackId).Distinct().ToList();
        var tracks = (await _trackRepo.ListAsync(t => trackIds.Contains(t.TrackId)))
            .ToDictionary(t => t.TrackId);
        var albumIds = tracks.Values.Select(t => t.AlbumId).Distinct().ToList();
        var albums = (await _albumRepo.ListAsync(a => albumIds.Contains(a.AlbumId)))
            .ToDictionary(a => a.AlbumId);

        var dtos = new List<FavoriteTrackDto>();
        foreach (var f in favorites)
        {
            if (!tracks.TryGetValue(f.TrackId, out var track)) continue;
            albums.TryGetValue(track.AlbumId, out var album);
            dtos.Add(new FavoriteTrackDto(track.TrackId, track.AlbumId, track.Title, track.Slug, track.Artists,
                track.TrackNumber, track.Duration, track.Genre, track.CoverPath, track.PlayCount,
                album?.Title ?? string.Empty, album?.CoverPath ?? string.Empty, f.AddedAt));
        }
        return dtos;
    }

    public async Task<IEnumerable<int>> GetFavoriteIdsAsync(Guid userId)
    {
        var favorites = await _favoriteRepo.ListAsync(f => f.UserId == userId);
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
    private readonly IRepository<MusicTrack> _trackRepo;
    private readonly IRepository<MusicAlbum> _albumRepo;

    public PlaylistService(IRepository<MusicPlaylist> playlistRepo, IRepository<PlaylistTrack> playlistTrackRepo,
        IRepository<MusicTrack> trackRepo, IRepository<MusicAlbum> albumRepo)
    {
        _playlistRepo = playlistRepo;
        _playlistTrackRepo = playlistTrackRepo;
        _trackRepo = trackRepo;
        _albumRepo = albumRepo;
    }

    public async Task<IEnumerable<PlaylistDto>> GetPlaylistsAsync(Guid userId)
    {
        var playlists = await _playlistRepo.ListAsync(p => p.UserId == userId, q => q.OrderByDescending(p => p.UpdatedAt));
        return playlists.Select(ToDto);
    }

    public Task<MusicPlaylist?> GetPlaylistAsync(Guid id) => _playlistRepo.GetByIdAsync(id);

    public async Task<(PlaylistOpResult result, PlaylistDetailDto? playlist)> GetPlaylistWithTracksAsync(Guid userId, Guid playlistId)
    {
        var playlist = await _playlistRepo.GetByIdAsync(playlistId);
        if (playlist == null) return (PlaylistOpResult.NotFound, null);
        if (playlist.UserId != userId && !playlist.IsPublic) return (PlaylistOpResult.Forbidden, null);

        // Ordered link rows → hydrate track + album details, preserving playlist order.
        var links = await _playlistTrackRepo.ListAsync(pt => pt.PlaylistId == playlistId,
            q => q.OrderBy(pt => pt.Position).ThenBy(pt => pt.AddedAt));
        var trackIds = links.Select(l => l.TrackId).ToList();
        var tracks = (await _trackRepo.ListAsync(t => trackIds.Contains(t.TrackId))).ToDictionary(t => t.TrackId);
        var albumIds = tracks.Values.Select(t => t.AlbumId).Distinct().ToList();
        var albums = (await _albumRepo.ListAsync(a => albumIds.Contains(a.AlbumId))).ToDictionary(a => a.AlbumId);

        var trackDtos = new List<MusicTrackDto>();
        foreach (var link in links)
        {
            if (!tracks.TryGetValue(link.TrackId, out var t)) continue; // track deleted since it was added
            albums.TryGetValue(t.AlbumId, out var album);
            trackDtos.Add(new MusicTrackDto(t.TrackId, t.AlbumId, t.Title, t.Slug, t.TrackNumber, t.Artists,
                t.Duration, t.Genre, t.CoverPath, t.PlayCount, t.IsFeatured, t.FileSize ?? 0,
                album?.Title, album?.CoverPath));
        }

        var dto = new PlaylistDetailDto(playlist.PlaylistId, playlist.Title, playlist.Description, playlist.IsPublic,
            playlist.TrackCount, playlist.CreatedAt, playlist.UpdatedAt, trackDtos);
        return (PlaylistOpResult.Ok, dto);
    }

    public async Task<PlaylistDto> CreatePlaylistAsync(Guid userId, string name, string? description)
    {
        var playlist = new MusicPlaylist { UserId = userId, Title = name, Description = description ?? string.Empty };
        var created = await _playlistRepo.AddAsync(playlist);
        return ToDto(created);
    }

    private static PlaylistDto ToDto(MusicPlaylist p) =>
        new(p.PlaylistId, p.Title, p.Description, p.IsPublic, p.TrackCount, p.CreatedAt, p.UpdatedAt);

    public async Task<PlaylistOpResult> AddTrackToPlaylistAsync(Guid userId, Guid playlistId, int trackId)
    {
        var playlist = await _playlistRepo.GetByIdAsync(playlistId);
        if (playlist == null) return PlaylistOpResult.NotFound;
        if (playlist.UserId != userId) return PlaylistOpResult.Forbidden;
        // Don't store links to tracks that don't exist (there's no FK on TrackId).
        if (await _trackRepo.GetByIdAsync(trackId) == null) return PlaylistOpResult.NotFound;

        var (existing, _) = await _playlistTrackRepo.GetPagedAsync(1, 1, pt => pt.PlaylistId == playlistId && pt.TrackId == trackId);
        if (existing.Count > 0) return PlaylistOpResult.Ok;

        // Append after the current last track (positions are 0-based).
        var current = await _playlistTrackRepo.ListAsync(pt => pt.PlaylistId == playlistId);
        var nextPos = current.Select(pt => pt.Position).DefaultIfEmpty(-1).Max() + 1;
        await _playlistTrackRepo.AddAsync(new PlaylistTrack { PlaylistId = playlistId, TrackId = trackId, Position = nextPos });
        await SyncTrackCountAsync(playlist, playlistId);
        return PlaylistOpResult.Ok;
    }

    private async Task SyncTrackCountAsync(MusicPlaylist playlist, Guid playlistId)
    {
        playlist.TrackCount = await _playlistTrackRepo.CountAsync(pt => pt.PlaylistId == playlistId);
        playlist.UpdatedAt = DateTime.UtcNow;
        await _playlistRepo.UpdateAsync(playlist);
    }

    public async Task<PlaylistOpResult> RemoveTrackFromPlaylistAsync(Guid userId, Guid playlistId, int trackId)
    {
        var playlist = await _playlistRepo.GetByIdAsync(playlistId);
        if (playlist == null) return PlaylistOpResult.NotFound;
        if (playlist.UserId != userId) return PlaylistOpResult.Forbidden;

        var (existing, _) = await _playlistTrackRepo.GetPagedAsync(1, 1, pt => pt.PlaylistId == playlistId && pt.TrackId == trackId);
        var pt = existing.FirstOrDefault();
        if (pt == null) return PlaylistOpResult.NotFound;
        await _playlistTrackRepo.DeleteAsync(pt);
        await SyncTrackCountAsync(playlist, playlistId);
        return PlaylistOpResult.Ok;
    }

    public async Task<PlaylistOpResult> ReorderPlaylistAsync(Guid userId, Guid playlistId, IReadOnlyList<int> trackIds)
    {
        var playlist = await _playlistRepo.GetByIdAsync(playlistId);
        if (playlist == null) return PlaylistOpResult.NotFound;
        if (playlist.UserId != userId) return PlaylistOpResult.Forbidden;

        var links = (await _playlistTrackRepo.ListAsync(pt => pt.PlaylistId == playlistId)).ToList();
        var byTrack = links.ToDictionary(l => l.TrackId);
        var pos = 0;
        // Apply the requested order first…
        foreach (var trackId in trackIds)
        {
            if (byTrack.TryGetValue(trackId, out var link))
            {
                link.Position = pos++;
                await _playlistTrackRepo.UpdateAsync(link);
            }
        }
        // …then append any links the client didn't include, keeping their prior relative order.
        foreach (var link in links.Where(l => !trackIds.Contains(l.TrackId)).OrderBy(l => l.Position))
        {
            link.Position = pos++;
            await _playlistTrackRepo.UpdateAsync(link);
        }

        playlist.UpdatedAt = DateTime.UtcNow;
        await _playlistRepo.UpdateAsync(playlist);
        return PlaylistOpResult.Ok;
    }

    public async Task<PlaylistOpResult> DeletePlaylistAsync(Guid userId, Guid playlistId)
    {
        var playlist = await _playlistRepo.GetByIdAsync(playlistId);
        if (playlist == null) return PlaylistOpResult.NotFound;
        if (playlist.UserId != userId) return PlaylistOpResult.Forbidden;
        // PlaylistTrack rows cascade-delete via the FK configured in MusicDbContext.
        await _playlistRepo.DeleteAsync(playlist);
        return PlaylistOpResult.Ok;
    }
}
