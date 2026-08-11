namespace Music.Service.Models;

public class UserFavorite
{
    public Guid UserId { get; set; }                  // no FK to identity
    public int TrackId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}