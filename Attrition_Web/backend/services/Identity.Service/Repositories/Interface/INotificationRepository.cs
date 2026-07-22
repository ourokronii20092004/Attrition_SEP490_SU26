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
    Task CreateAsync(CreateNotificationRequest request);
}
