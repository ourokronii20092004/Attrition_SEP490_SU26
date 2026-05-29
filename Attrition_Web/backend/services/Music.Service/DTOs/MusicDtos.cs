using Microsoft.AspNetCore.Http;

namespace Music.Service.DTOs;

public record CreateAlbumRequest(
    string Title,
    List<string>? Artists,
    string? Description,
    string? Genre,
    string? AlbumType,
    DateTime? ReleaseDate,
    int SortOrder = 0
);

public class UploadTrackRequest
{
    public int? AlbumId { get; set; }
    public string? Title { get; set; }
    public List<string>? Artists { get; set; }
    public int? TrackNumber { get; set; }
    public int? Duration { get; set; }
    public string? Genre { get; set; }
    public bool IsFeatured { get; set; }
    public IFormFile? File { get; set; }
    public string? TempFileKey { get; set; }
    public string? TempCoverPath { get; set; }
    public IFormFile? CoverFile { get; set; }
}

public record UpdateTrackRequest(
    string? Title,
    List<string>? Artists,
    int? TrackNumber,
    int? Duration,
    string? Genre,
    bool? IsFeatured
);

public record MusicAlbumDto(
    int AlbumId, string Title, string Slug, List<string> Artists, string? Description,
    string? CoverPath, bool IsCoverUserDefined, DateTime? ReleaseDate, string AlbumType, string? Genre,
    int TrackCount, int TotalDuration, DateTime CreatedAt
);

public record MusicTrackDto(
    int TrackId, int AlbumId, string Title, string Slug, int TrackNumber, List<string> Artists,
    int Duration, string? Genre, string? CoverPath, int PlayCount, bool IsFeatured, long FileSize,
    string? AlbumTitle = null, string? AlbumCoverPath = null
);

public record AlbumDetailDto(
    int AlbumId, string Title, string Slug, List<string> Artists, string? Description,
    string? CoverPath, bool IsCoverUserDefined, DateTime? ReleaseDate, string AlbumType, string? Genre,
    int TrackCount, int TotalDuration, DateTime CreatedAt, IEnumerable<MusicTrackDto> Tracks
);

public record NewestAlbumDto(
    int AlbumId, string Title, string? CoverPath, List<string> Artists, int TrackCount, DateTime NewestTrackAddedAt
);

public record FeaturedTracksResponse(
    IEnumerable<MusicTrackDto> FeaturedTracks, IEnumerable<NewestAlbumDto> NewestAlbums
);

public record ScanTrackResponse(
    string TempFileKey, string Title, string? Album, List<string> Artists, string? Genre,
    int TrackNumber, int Duration, string? TempCoverPath
);

public record FavoriteTrackDto(
    int TrackId, int AlbumId, string Title, string Slug, List<string> Artists, int TrackNumber,
    int Duration, string? Genre, string? CoverPath, int PlayCount, string AlbumTitle, string AlbumCoverPath,
    DateTime FavoritedAt
);

public record CreatePlaylistReq(string Name, string? Description);
public record AddTrackToPlaylistReq(int TrackId);
