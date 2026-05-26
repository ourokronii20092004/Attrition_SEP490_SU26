using System;

namespace Attrition.API.Models;

public class ThreadSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ThreadId { get; set; }
    public ForumThread Thread { get; set; } = null!;
    
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;
}
