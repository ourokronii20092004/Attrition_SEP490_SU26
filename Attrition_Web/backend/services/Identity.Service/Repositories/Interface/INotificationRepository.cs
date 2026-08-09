using BuildingBlocks.Contracts;
using Identity.Service.DTOs;

namespace Identity.Service.Repositories.Interface;

public interface INotificationRepository
{
    Task<List<NotificationDto>> ListAsync(Guid userId, int limit);

    Task<PaginatedResponse<NotificationDto>> ListPagedAsync(Guid userId, int page, int pageSize, bool unreadOnly);

    Task<int> UnreadCountAsync(Guid userId);

    Task MarkReadAsync(Guid userId, Guid notificationId);

    Task MarkAllReadAsync(Guid userId);

    /// <summary>Marks read every notification of this user's that deep-links into the given thread. Returns how many were cleared.</summary>
    Task<int> MarkThreadReadAsync(Guid userId, Guid threadId);

    Task CreateAsync(CreateNotificationRequest request);

    Task CreateManyAsync(CreateNotificationsBulkRequest request);
}