namespace Forum.Service.Models;

public class ThreadSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ThreadId { get; set; }
    public Guid UserId { get; set; }
    public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;
}
