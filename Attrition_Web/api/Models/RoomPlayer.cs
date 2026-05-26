namespace Attrition.API.Models;

public class RoomPlayer
{
    public Guid RoomId { get; set; }
    public GameRoom GameRoom { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid? CharacterId { get; set; }
    public Character? Character { get; set; }

    public short PlayerRole { get; set; } = 0; // 0=guest, 1=host
    public bool IsReady { get; set; } = false;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}