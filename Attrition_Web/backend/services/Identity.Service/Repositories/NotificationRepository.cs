using BuildingBlocks.Contracts;
using Identity.Service.Data;
using Identity.Service.DTOs;
using Identity.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Identity.Service.Repositories;

public class NotificationRepository(IdentityDbContext db) : INotificationRepository
{
    public Task<List<NotificationDto>> ListAsync(Guid userId, int limit) => db.Notifications
        .Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).Take(limit)
        .Select(n => new NotificationDto(n.Id, n.Type, n.Message, n.Link, n.ActorName, n.IsRead, n.CreatedAt)).ToListAsync();

    public async Task<PaginatedResponse<NotificationDto>> ListPagedAsync(Guid userId, int page, int pageSize, bool unreadOnly)
    {
        var query = db.Notifications.Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(n => n.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Message, n.Link, n.ActorName, n.IsRead, n.CreatedAt)).ToListAsync();
        return new(items, total, page, pageSize);
    }

    public Task<int> UnreadCountAsync(Guid userId) => db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

    public Task MarkReadAsync(Guid userId, Guid notificationId) => db.Notifications.Where(n => n.Id == notificationId && n.UserId == userId)
        .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

    public Task MarkAllReadAsync(Guid userId) => db.Notifications.Where(n => n.UserId == userId && !n.IsRead)
        .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

    /// <summary>
    /// Clears the backlog for one thread, so muting it also silences the notifications it already
    /// produced. Matched on the deep link (<c>/forum/{threadId}...</c>) because notifications store
    /// a link rather than a thread id — Identity owns this table and has no forum schema to join.
    /// </summary>
    public Task<int> MarkThreadReadAsync(Guid userId, Guid threadId)
    {
        var prefix = $"/forum/{threadId}";
        return db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead && n.Link != null && n.Link.StartsWith(prefix))
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    public async Task CreateAsync(CreateNotificationRequest request)
    {
        var user = request.UserId is { } id
            ? await db.Users.FirstOrDefaultAsync(u => u.Id == id)
            : !string.IsNullOrWhiteSpace(request.Username)
                ? await db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username!.ToLower())
                : null;
        if (user == null) return;
        if (request.Type == NotificationType.Reply && !user.NotifyOnReply) return;
        if (request.Type == NotificationType.Mention && !user.NotifyOnMention) return;
        db.Notifications.Add(new Notification
        {
            UserId = user.Id,
            Type = request.Type,
            Message = request.Message,
            Link = request.Link,
            ActorName = request.ActorName
        });
        await db.SaveChangesAsync();
    }

    public async Task CreateManyAsync(CreateNotificationsBulkRequest request)
    {
        var ids = request.UserIds.Distinct().ToList();
        if (ids.Count == 0) return;

        // Resolve in one round-trip and honour the same per-user opt-outs as the single create.
        var recipients = await db.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.NotifyOnReply, u.NotifyOnMention })
            .ToListAsync();

        var wanted = recipients.Where(u => request.Type switch
        {
            NotificationType.Reply => u.NotifyOnReply,
            NotificationType.Mention => u.NotifyOnMention,
            _ => true,
        }).Select(u => u.Id).ToList();
        if (wanted.Count == 0) return;

        db.Notifications.AddRange(wanted.Select(userId => new Notification
        {
            UserId = userId,
            Type = request.Type,
            Message = request.Message,
            Link = request.Link,
            ActorName = request.ActorName,
        }));
        await db.SaveChangesAsync();
    }
}