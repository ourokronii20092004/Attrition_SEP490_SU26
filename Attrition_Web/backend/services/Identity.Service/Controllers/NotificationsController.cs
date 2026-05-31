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
}
