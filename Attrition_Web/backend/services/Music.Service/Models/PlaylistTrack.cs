namespace Music.Service.Models;

public class PlaylistTrack
{
    public Guid PlaylistId { get; set; }
    public int TrackId { get; set; }
    public int Position { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}