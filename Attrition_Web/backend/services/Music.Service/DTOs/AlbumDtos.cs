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

public record MusicAlbumDto(
    int AlbumId, string Title, string Slug, List<string> Artists, string? Description,
    string? CoverPath, bool IsCoverUserDefined, DateTime? ReleaseDate, string AlbumType, string? Genre,
    int TrackCount, int TotalDuration, DateTime CreatedAt, int SortOrder = 0
);

public record AlbumDetailDto(
    int AlbumId, string Title, string Slug, List<string> Artists, string? Description,
    string? CoverPath, bool IsCoverUserDefined, DateTime? ReleaseDate, string AlbumType, string? Genre,
    int TrackCount, int TotalDuration, DateTime CreatedAt, IEnumerable<MusicTrackDto> Tracks
);

public record NewestAlbumDto(
    int AlbumId, string Title, string? CoverPath, List<string> Artists, int TrackCount, DateTime NewestTrackAddedAt
);