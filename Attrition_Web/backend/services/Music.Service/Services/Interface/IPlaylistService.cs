using Music.Service.DTOs;
using Music.Service.Models;

namespace Music.Service.Services.Interface;

public enum PlaylistOpResult
{ Ok, NotFound, Forbidden }

public interface IPlaylistService
{
    Task<IEnumerable<PlaylistDto>> GetPlaylistsAsync(Guid userId);

    Task<MusicPlaylist?> GetPlaylistAsync(Guid id);

    Task<(PlaylistOpResult result, PlaylistDetailDto? playlist)> GetPlaylistWithTracksAsync(Guid userId, Guid playlistId);

    Task<PlaylistDto> CreatePlaylistAsync(Guid userId, string name, string? description);

    Task<PlaylistOpResult> AddTrackToPlaylistAsync(Guid userId, Guid playlistId, int trackId);

    Task<PlaylistOpResult> RemoveTrackFromPlaylistAsync(Guid userId, Guid playlistId, int trackId);

    Task<PlaylistOpResult> ReorderPlaylistAsync(Guid userId, Guid playlistId, IReadOnlyList<int> trackIds);

    Task<PlaylistOpResult> DeletePlaylistAsync(Guid userId, Guid playlistId);
}