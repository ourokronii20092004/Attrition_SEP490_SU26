namespace Attrition.API.Models;

public class GameRoom
{
    public Guid RoomId { get; set; } = Guid.NewGuid();
    public string RoomCode { get; set; } = string.Empty;
    public Guid HostUserId { get; set; }
    public User HostUser { get; set; } = null!;
    public string? RoomName { get; set; }
    public short MaxPlayers { get; set; } = 4;
    public string? CurrentScene { get; set; }
    public string Status { get; set; } = RoomStatus.Waiting;
    public bool IsPrivate { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }

    public ICollection<RoomPlayer> Players { get; set; } = new List<RoomPlayer>();
}