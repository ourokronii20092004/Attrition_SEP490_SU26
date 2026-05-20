namespace Attrition.API.Models;

public class PlaylistTrack
{
    public Guid PlaylistId { get; set; }
    public MusicPlaylist Playlist { get; set; } = null!;
    public int TrackId { get; set; }
    public MusicTrack Track { get; set; } = null!;
    public int Position { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}