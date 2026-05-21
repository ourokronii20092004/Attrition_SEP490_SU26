namespace Attrition.API.DTOs;

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
