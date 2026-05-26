using Attrition.API.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Attrition.API.Services;

public interface IPlaylistService
{
    Task<IEnumerable<MusicPlaylist>> GetPlaylistsAsync(Guid userId);
    Task<MusicPlaylist?> GetPlaylistAsync(Guid id);
    Task<MusicPlaylist> CreatePlaylistAsync(Guid userId, string name, string? description);
    Task<bool> AddTrackToPlaylistAsync(Guid playlistId, int trackId);
    Task<bool> RemoveTrackFromPlaylistAsync(Guid playlistId, int trackId);
}
