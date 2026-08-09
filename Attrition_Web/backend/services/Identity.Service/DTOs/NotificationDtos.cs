namespace Identity.Service.DTOs;

public record NotificationDto(Guid Id, string Type, string Message, string? Link, string? ActorName,
    bool IsRead, DateTime CreatedAt);

/// <summary>Service-to-service create payload (Forum → Identity on reply/mention). Target the
/// recipient by UserId (replies — Forum knows the parent author's id) OR Username (@mentions —
/// Identity resolves it, since it owns users). Exactly one is required.</summary>
public record CreateNotificationRequest(string Type, string Message, string? Link, string? ActorName,
    Guid? UserId = null, string? Username = null);

/// <summary>Fan-out create: one row per recipient, all sharing the same text. Used for thread
/// subscriber notifications, where a per-recipient HTTP call would put one round-trip per
/// subscriber on the reply request's critical path.</summary>
public record CreateNotificationsBulkRequest(string Type, string Message, string? Link,
    string? ActorName, List<Guid> UserIds);