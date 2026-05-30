using Music.Service.DTOs;
using Music.Service.Models;

namespace Music.Service.Services;

public interface IAlbumService
{
    Task<IEnumerable<MusicAlbumDto>> GetAlbumsAsync();
    Task<AlbumDetailDto?> GetAlbumAsync(int id);
    Task<MusicAlbumDto> CreateAlbumAsync(CreateAlbumRequest req);
    Task<MusicAlbumDto?> UpdateAlbumAsync(int id, CreateAlbumRequest req);
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

public enum PlaylistOpResult { Ok, NotFound, Forbidden }

public interface IPlaylistService
{
    Task<IEnumerable<PlaylistDto>> GetPlaylistsAsync(Guid userId);
    Task<MusicPlaylist?> GetPlaylistAsync(Guid id);
    Task<PlaylistDto> CreatePlaylistAsync(Guid userId, string name, string? description);
    Task<PlaylistOpResult> AddTrackToPlaylistAsync(Guid userId, Guid playlistId, int trackId);
    Task<PlaylistOpResult> RemoveTrackFromPlaylistAsync(Guid userId, Guid playlistId, int trackId);
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

    private static readonly string[] AudioExtensions = { ".mp3", ".flac", ".ogg", ".m4a", ".wav", ".aac" };
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    public static bool IsAllowedAudioExtension(string? fileName) =>
        HasAllowedExtension(fileName, AudioExtensions);

    public static bool IsAllowedImageExtension(string? fileName) =>
        HasAllowedExtension(fileName, ImageExtensions);

    /// <summary>
    /// Sniffs the leading bytes of an image stream to confirm it is a real raster image
    /// (JPEG/PNG/GIF/WebP). Rejects text-based payloads (SVG/HTML) that a renamed extension
    /// could otherwise smuggle past the extension check. Leaves the stream position reset to 0.
    /// </summary>
    public static async Task<bool> LooksLikeImageAsync(Stream stream)
    {
        if (stream == null || !stream.CanRead) return false;
        var header = new byte[12];
        if (stream.CanSeek) stream.Position = 0;
        int read = await stream.ReadAsync(header.AsMemory(0, header.Length));
        if (stream.CanSeek) stream.Position = 0;
        if (read < 12) return false;

        // JPEG: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return true;
        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) return true;
        // GIF: "GIF8"
        if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38) return true;
        // WEBP: "RIFF"...."WEBP"
        if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50) return true;
        return false;
    }

    private static bool HasAllowedExtension(string? fileName, string[] allowed)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return allowed.Contains(ext);
    }

    /// <summary>
    /// Resolves <paramref name="candidate"/> (a file name supplied by a client) against
    /// <paramref name="baseDir"/> and returns the full path only if it stays inside the base directory.
    /// Returns null if the candidate escapes the base directory (path traversal) or is empty.
    /// </summary>
    public static string? ResolveContainedPath(string baseDir, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return null;

        // Collapse to a bare file name first so separators / "../" segments cannot escape.
        var safeName = Path.GetFileName(candidate);
        if (string.IsNullOrEmpty(safeName)) return null;

        var baseFull = Path.GetFullPath(baseDir);
        var combined = Path.GetFullPath(Path.Combine(baseFull, safeName));

        var baseWithSep = baseFull.EndsWith(Path.DirectorySeparatorChar)
            ? baseFull
            : baseFull + Path.DirectorySeparatorChar;
        return combined.StartsWith(baseWithSep, StringComparison.Ordinal) ? combined : null;
    }
}
