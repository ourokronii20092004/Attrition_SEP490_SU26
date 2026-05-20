namespace Attrition.API.Models;

public class MusicTrack
{
    public int TrackId { get; set; }
    public int AlbumId { get; set; }
    public MusicAlbum Album { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int TrackNumber { get; set; }
    public int Duration { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public string? Genre { get; set; }
    public int? Bpm { get; set; }
    public int PlayCount { get; set; } = 0;
    public bool IsFeatured { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}