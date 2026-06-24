using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Character.Service.DTOs;
using Character.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Character.Service.Controllers;

/// <summary>
/// Player-facing rooms (saved journeys). Host-authoritative: the host owns the room and writes
/// progress for every player in it. Authenticated with the host's JWT (no shared internal key);
/// every write is guarded so a host may only touch a room they own. Joining clients may look a
/// room up by code (read-only) so they can enter it.
/// </summary>
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

    /// <summary>A joining client looks a room up by the code the host shared, to enter it.</summary>
    [HttpGet("by-code/{roomCode}")]
    public async Task<IActionResult> GetByCode(string roomCode)
    {
        if (this.RequireUserId(_user, out _) is { } error) return error;
        var session = await _service.GetByRoomCodeAsync(roomCode);
        return session == null
            ? NotFound(ApiResponse.Fail("Room not found."))
            : Ok(ApiResponse<SessionDetailDto>.Ok(session));
    }

    /// <summary>Host creates a new room or re-opens an existing one (by fixed RoomCode they own).</summary>
    [HttpPost]
    public async Task<IActionResult> CreateOrReopen([FromBody] CreateSessionRequest request)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        // Owner is taken from the JWT, never trusted from the body.
        var result = await _service.CreateOrReopenAsync(request with { OwnerId = userId });
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Host updates room-level metadata (playtime, current scene) on save/quit.</summary>
    [HttpPost("meta")]
    public async Task<IActionResult> UpdateMeta([FromBody] UpdateSessionRequest request)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        if (await RequireOwnership(request.SessionId, userId) is { } denied) return denied;
        var result = await _service.UpdateMetaAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Host upserts one character's progress for a room (keyed on CharacterId + SessionId).</summary>
    [HttpPost("character")]
    public async Task<IActionResult> SaveCharacter([FromBody] SaveCharacterSessionRequest request)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        if (await RequireOwnership(request.SessionId, userId) is { } denied) return denied;
        var result = await _service.SaveCharacterSessionAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Host upserts world/quest progress for a room (keyed on SessionId + EventId).</summary>
    [HttpPost("world-state")]
    public async Task<IActionResult> SaveWorldState([FromBody] SaveWorldStateRequest request)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        if (await RequireOwnership(request.SessionId, userId) is { } denied) return denied;
        var result = await _service.SaveWorldStateAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Host deletes a room entirely.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteSession(Guid id)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        if (await RequireOwnership(id, userId) is { } denied) return denied;
        var result = await _service.DeleteSessionAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Guard: the caller must own the room before writing to it (admins bypass). Returns null when
    /// allowed, a 403/404 result otherwise. Keeps every write endpoint a single line.
    /// </summary>
    private async Task<IActionResult?> RequireOwnership(Guid sessionId, Guid userId)
    {
        if (_user.IsAdmin) return null;
        var ownerId = await _service.GetOwnerIdAsync(sessionId);
        if (ownerId == null)
            return NotFound(ApiResponse.Fail("Room not found."));
        if (ownerId != userId)
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("You do not own this room."));
        return null;
    }
}
