namespace Music.Service.DTOs;

public record CreatePlaylistReq(string Name, string? Description);
public record AddTrackToPlaylistReq(int TrackId);
public record ReorderPlaylistReq(List<int> TrackIds);

public record PlaylistDto(
    Guid PlaylistId, string Title, string? Description, bool IsPublic, int TrackCount,
    DateTime CreatedAt, DateTime UpdatedAt
);

public record PlaylistDetailDto(
    Guid PlaylistId, string Title, string? Description, bool IsPublic, int TrackCount,
    DateTime CreatedAt, DateTime UpdatedAt, IEnumerable<MusicTrackDto> Tracks
);