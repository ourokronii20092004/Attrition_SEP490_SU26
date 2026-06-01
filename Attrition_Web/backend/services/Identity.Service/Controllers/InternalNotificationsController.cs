using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Identity.Service.DTOs;
using Identity.Service.Services;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Service.Controllers;

/// <summary>
/// Service-to-service notification creation (Forum → Identity on reply/mention).
/// Guarded by the shared internal key, not user JWT. Identity owns the notifications table
/// because it owns the user + the notify preferences.
/// </summary>
[ApiController]
[Route("api/internal/notifications")]
public class InternalNotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;
    private readonly IConfiguration _config;

    public InternalNotificationsController(INotificationService notifications, IConfiguration config)
    {
        _notifications = notifications;
        _config = config;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateNotificationRequest request)
    {
        if (!InternalKey.Validate(Request, _config))
            return Unauthorized(ApiResponse.Fail("Valid service authentication is required."));
        if (request is null || string.IsNullOrWhiteSpace(request.Type)
            || (request.UserId is null && string.IsNullOrWhiteSpace(request.Username)))
            return BadRequest(ApiResponse.Fail("A valid notification payload (type + userId or username) is required."));
        await _notifications.CreateAsync(request);
        return Ok(ApiResponse.Ok());
    }
}
