namespace Attrition.API.Models;

public class UserFavorite
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public int TrackId { get; set; }
    public MusicTrack Track { get; set; } = null!;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}