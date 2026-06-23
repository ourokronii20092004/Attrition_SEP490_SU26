namespace Music.Service.Models;

public class MusicPlaylist
{
    public Guid PlaylistId { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }                  // no FK to identity
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public int TrackCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PlaylistTrack> Tracks { get; set; } = new List<PlaylistTrack>();
}
