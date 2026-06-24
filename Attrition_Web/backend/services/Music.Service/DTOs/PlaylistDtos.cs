namespace Music.Service.DTOs;

public record CreatePlaylistReq(string Name, string? Description);
public record AddTrackToPlaylistReq(int TrackId);

public record PlaylistDto(
    Guid PlaylistId, string Title, string? Description, bool IsPublic, int TrackCount,
    DateTime CreatedAt, DateTime UpdatedAt
);
