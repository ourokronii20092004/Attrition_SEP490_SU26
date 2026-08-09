namespace Music.Service.Models;

public class MusicTrack
{
    public int TrackId { get; set; }
    public int AlbumId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public List<string> Artists { get; set; } = new();
    public string? CoverPath { get; set; }
    public int TrackNumber { get; set; }
    public int Duration { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public string? Genre { get; set; }
    public int? Bpm { get; set; }
    public int PlayCount { get; set; }
    public bool IsFeatured { get; set; }
    public string? UnitySourceKey { get; set; }
    public List<string> GameUsages { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}