using BuildingBlocks.Contracts;
using Identity.Service.DTOs;

namespace Identity.Service.Services;

public class NotificationService(INotificationRepository repository) : INotificationService
{
    public Task<List<NotificationDto>> ListAsync(Guid userId, int limit) =>
        repository.ListAsync(userId, Math.Clamp(limit, 1, 50));

    public Task<int> UnreadCountAsync(Guid userId) => repository.UnreadCountAsync(userId);

    public Task<PaginatedResponse<NotificationDto>> ListPagedAsync(Guid userId, int page, int pageSize, bool unreadOnly) =>
        repository.ListPagedAsync(userId, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), unreadOnly);

    public Task MarkReadAsync(Guid userId, Guid notificationId) => repository.MarkReadAsync(userId, notificationId);
    public Task MarkAllReadAsync(Guid userId) => repository.MarkAllReadAsync(userId);
    public Task CreateAsync(CreateNotificationRequest request) => repository.CreateAsync(request);
}
