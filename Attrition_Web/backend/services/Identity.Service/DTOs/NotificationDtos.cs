namespace Identity.Service.DTOs;

// ─── Notifications ───
public record NotificationDto(Guid Id, string Type, string Message, string? Link, string? ActorName,
    bool IsRead, DateTime CreatedAt);

/// <summary>Service-to-service create payload (Forum → Identity on reply/mention). Target the
/// recipient by UserId (replies — Forum knows the parent author's id) OR Username (@mentions —
/// Identity resolves it, since it owns users). Exactly one is required.</summary>
public record CreateNotificationRequest(string Type, string Message, string? Link, string? ActorName,
    Guid? UserId = null, string? Username = null);
