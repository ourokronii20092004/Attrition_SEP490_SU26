namespace Attrition.API.Models;

public class MusicAlbum
{
    public int AlbumId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public List<string> Artists { get; set; } = new();
    public string? Description { get; set; }
    public string? CoverPath { get; set; }
    public bool IsCoverUserDefined { get; set; } = false;
    public string? Genre { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public string AlbumType { get; set; } = "soundtrack";
    public int TrackCount { get; set; } = 0;
    public int TotalDuration { get; set; } = 0;
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MusicTrack> Tracks { get; set; } = new List<MusicTrack>();
}