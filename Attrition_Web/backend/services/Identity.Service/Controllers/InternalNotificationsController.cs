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
    /// <summary>Upper bound on a single fan-out. Threads never legitimately have this many
    /// subscribers; the cap keeps one request from writing unbounded rows.</summary>
    private const int MaxBulkRecipients = 500;

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

    /// <summary>
    /// Fan-out create: one notification per recipient, all sharing the same text. Keeps a thread
    /// with many subscribers from costing one HTTP round-trip per subscriber on the reply path.
    /// </summary>
    [HttpPost("bulk")]
    public async Task<IActionResult> CreateMany([FromBody] CreateNotificationsBulkRequest request)
    {
        if (!InternalKey.Validate(Request, _config))
            return Unauthorized(ApiResponse.Fail("Valid service authentication is required."));
        if (request is null || string.IsNullOrWhiteSpace(request.Type) || request.UserIds is null)
            return BadRequest(ApiResponse.Fail("A valid notification payload (type + userIds) is required."));
        // Bound the fan-out so a pathological thread can't be used to write unbounded rows.
        if (request.UserIds.Count > MaxBulkRecipients)
            return BadRequest(ApiResponse.Fail($"At most {MaxBulkRecipients} recipients per request."));
        await _notifications.CreateManyAsync(request);
        return Ok(ApiResponse.Ok());
    }
}
