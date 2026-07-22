using BuildingBlocks.Contracts;
using Identity.Service.DTOs;

namespace Identity.Service.Services.Interface;

public interface INotificationService
{
    Task<List<NotificationDto>> ListAsync(Guid userId, int limit);
    Task<PaginatedResponse<NotificationDto>> ListPagedAsync(Guid userId, int page, int pageSize, bool unreadOnly);
    Task<int> UnreadCountAsync(Guid userId);
    Task MarkReadAsync(Guid userId, Guid notificationId);
    Task MarkAllReadAsync(Guid userId);
    Task CreateAsync(CreateNotificationRequest request);
}
