using Music.Service.DTOs;
using Music.Service.Models;

namespace Music.Service.Services;

public interface IAlbumService
{
    Task<IEnumerable<MusicAlbumDto>> GetAlbumsAsync();
    Task<AlbumDetailDto?> GetAlbumAsync(int id);
    Task<MusicAlbum> CreateAlbumAsync(CreateAlbumRequest req);
    Task<MusicAlbum?> UpdateAlbumAsync(int id, CreateAlbumRequest req);
    Task<bool> DeleteAlbumAsync(int id);
    Task<(bool success, string? error, string? coverPath)> UploadAlbumCoverAsync(int id, Microsoft.AspNetCore.Http.IFormFile file);
    Task<int> CountAsync();
}

public interface ITrackService
{
    Task<IEnumerable<MusicTrackDto>> GetTracksAsync(int? albumId);
    Task<FeaturedTracksResponse> GetFeaturedTracksAsync();
    Task<(string? filePath, bool trackExists)> GetTrackStreamInfoAsync(int id);
    Task<bool> IncrementPlayCountAsync(int id);
    Task<(bool success, string? error, ScanTrackResponse? data)> ScanTrackAsync(Microsoft.AspNetCore.Http.IFormFile file);
    Task<(bool success, string? error, MusicTrackDto? data)> UploadTrackAsync(UploadTrackRequest req);
    Task<(bool success, string? error, MusicTrackDto? data)> UpdateTrackAsync(int id, UpdateTrackRequest req);
    Task<bool> DeleteTrackAsync(int id);
    Task<int> CountAsync();
}

public interface IFavoriteService
{
    Task<IEnumerable<FavoriteTrackDto>> GetFavoritesAsync(Guid userId);
    Task<IEnumerable<int>> GetFavoriteIdsAsync(Guid userId);
    Task<(bool success, bool isFavorited, string? error)> ToggleFavoriteAsync(Guid userId, int trackId);
}

public interface IPlaylistService
{
    Task<IEnumerable<MusicPlaylist>> GetPlaylistsAsync(Guid userId);
    Task<MusicPlaylist?> GetPlaylistAsync(Guid id);
    Task<MusicPlaylist> CreatePlaylistAsync(Guid userId, string name, string? description);
    Task<bool> AddTrackToPlaylistAsync(Guid playlistId, int trackId);
    Task<bool> RemoveTrackFromPlaylistAsync(Guid playlistId, int trackId);
}

/// <summary>Shared helpers for music services.</summary>
public static class MusicHelpers
{
    public static List<string> ParseArtists(IEnumerable<string>? input)
    {
        if (input == null) return new List<string>();
        var result = new List<string>();
        var separators = new[] { ',', '/', ';', '|', '\\' };
        foreach (var entry in input)
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            foreach (var s in entry.Split(separators, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = s.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !result.Contains(trimmed))
                    result.Add(trimmed);
            }
        }
        return result;
    }
}
