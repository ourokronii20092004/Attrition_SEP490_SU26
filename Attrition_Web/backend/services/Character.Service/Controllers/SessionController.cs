using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Character.Service.DTOs;
using Character.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Character.Service.Controllers;

/// <summary>Player-facing room (saved journey) reads. A host sees only rooms they own.</summary>
[ApiController]
[Route("api/sessions")]
[Authorize]
public class SessionController : ControllerBase
{
    private readonly ISessionService _service;
    private readonly ICurrentUser _user;

    public SessionController(ISessionService service, ICurrentUser user)
    {
        _service = service;
        _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        return Ok(ApiResponse<List<SessionSummaryDto>>.Ok(await _service.GetByOwnerAsync(userId)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var session = await _service.GetDetailAsync(id);
        if (session == null) return NotFound(ApiResponse.Fail("Room not found."));
        // Ownership guard: a host may only read their own rooms; admins may read any.
        if (session.OwnerId != userId && !_user.IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("You do not have access to this room."));
        return Ok(ApiResponse<SessionDetailDto>.Ok(session));
    }
}
