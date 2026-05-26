using Attrition.API.Models;
using Attrition.API.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Attrition.API.Services;

public class PlaylistService : IPlaylistService
{
    private readonly IRepository<MusicPlaylist> _playlistRepo;
    private readonly IRepository<PlaylistTrack> _playlistTrackRepo;

    public PlaylistService(
        IRepository<MusicPlaylist> playlistRepo,
        IRepository<PlaylistTrack> playlistTrackRepo)
    {
        _playlistRepo = playlistRepo;
        _playlistTrackRepo = playlistTrackRepo;
    }

    public async Task<IEnumerable<MusicPlaylist>> GetPlaylistsAsync(Guid userId)
    {
        var (playlists, _) = await _playlistRepo.GetPagedAsync(1, int.MaxValue, p => p.UserId == userId);
        return playlists;
    }

    public async Task<MusicPlaylist?> GetPlaylistAsync(Guid id)
    {
        return await _playlistRepo.GetByIdAsync(id);
    }

    public async Task<MusicPlaylist> CreatePlaylistAsync(Guid userId, string name, string? description)
    {
        var playlist = new MusicPlaylist
        {
            UserId = userId,
            Title = name,
            Description = description ?? string.Empty
        };
        return await _playlistRepo.AddAsync(playlist);
    }


    public async Task<bool> AddTrackToPlaylistAsync(Guid playlistId, int trackId)
    {
        var (existing, _) = await _playlistTrackRepo.GetPagedAsync(1, 1, pt => pt.PlaylistId == playlistId && pt.TrackId == trackId);
        if (existing.Count > 0) return true; // Already added

        var pt = new PlaylistTrack { PlaylistId = playlistId, TrackId = trackId };
        await _playlistTrackRepo.AddAsync(pt);
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
