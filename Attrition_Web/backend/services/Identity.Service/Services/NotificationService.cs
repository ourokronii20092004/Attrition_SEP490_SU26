using BuildingBlocks.Contracts;
using Identity.Service.Data;
using Identity.Service.DTOs;
using Identity.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Identity.Service.Services;

public interface INotificationService
{
    Task<List<NotificationDto>> ListAsync(Guid userId, int limit);
    Task<int> UnreadCountAsync(Guid userId);
    Task MarkReadAsync(Guid userId, Guid notificationId);
    Task MarkAllReadAsync(Guid userId);
    Task CreateAsync(CreateNotificationRequest request);
}

public class NotificationService : INotificationService
{
    private readonly IdentityDbContext _db;
    public NotificationService(IdentityDbContext db) => _db = db;

    public async Task<List<NotificationDto>> ListAsync(Guid userId, int limit)
    {
        limit = Math.Clamp(limit, 1, 50);
        return await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Message, n.Link, n.ActorName, n.IsRead, n.CreatedAt))
            .ToListAsync();
    }

    public Task<int> UnreadCountAsync(Guid userId) =>
        _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

    public async Task MarkReadAsync(Guid userId, Guid notificationId)
    {
        // Scoped to the owner so a user can't mark someone else's notification read.
        await _db.Notifications
            .Where(n => n.Id == notificationId && n.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    public async Task MarkAllReadAsync(Guid userId)
    {
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    public async Task CreateAsync(CreateNotificationRequest request)
    {
        // Resolve the recipient by id (replies) or username (mentions, case-insensitive).
        var user = request.UserId is { } id
            ? await _db.Users.FirstOrDefaultAsync(u => u.Id == id)
            : !string.IsNullOrWhiteSpace(request.Username)
                ? await _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username!.ToLower())
                : null;
        if (user == null) return;

        // Respect the recipient's per-type preference (mute).
        if (request.Type == NotificationType.Reply && !user.NotifyOnReply) return;
        if (request.Type == NotificationType.Mention && !user.NotifyOnMention) return;

        _db.Notifications.Add(new Notification
        {
            UserId = user.Id,
            Type = request.Type,
            Message = request.Message,
            Link = request.Link,
            ActorName = request.ActorName,
        });
        await _db.SaveChangesAsync();
    }
}
