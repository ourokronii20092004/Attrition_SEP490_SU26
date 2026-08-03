using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Identity.Service.DTOs;
using Identity.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Service.Controllers;

/// <summary>The signed-in user's own notifications (bell). JWT-scoped to the caller.</summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;
    private readonly ICurrentUser _user;

    public NotificationsController(INotificationService notifications, ICurrentUser user)
    {
        _notifications = notifications;
        _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int limit = 20)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        return Ok(ApiResponse<List<NotificationDto>>.Ok(await _notifications.ListAsync(userId, limit)));
    }

    [HttpGet("paged")]
    public async Task<IActionResult> ListPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] bool unreadOnly = false)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        return Ok(ApiResponse<PaginatedResponse<NotificationDto>>.Ok(
            await _notifications.ListPagedAsync(userId, page, pageSize, unreadOnly)));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        return Ok(ApiResponse<int>.Ok(await _notifications.UnreadCountAsync(userId)));
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        await _notifications.MarkReadAsync(userId, id);
        return Ok(ApiResponse.Ok());
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        await _notifications.MarkAllReadAsync(userId);
        return Ok(ApiResponse.Ok());
    }

    /// <summary>
    /// Clears this user's unread notifications for one thread. Paired with muting: silencing a
    /// thread should also clear the pile it already produced, not just stop new ones.
    /// </summary>
    [HttpPut("thread/{threadId:guid}/read")]
    public async Task<IActionResult> MarkThreadRead(Guid threadId)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var cleared = await _notifications.MarkThreadReadAsync(userId, threadId);
        return Ok(ApiResponse<int>.Ok(cleared));
    }
}
