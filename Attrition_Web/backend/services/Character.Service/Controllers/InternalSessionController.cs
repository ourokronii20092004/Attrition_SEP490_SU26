using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Character.Service.DTOs;
using Character.Service.Services;
using Microsoft.AspNetCore.Mvc;

namespace Character.Service.Controllers;

/// <summary>
/// Game-client room ingestion. The trusted Unity host creates/re-opens rooms and pushes per-room
/// progress (character_session, world_state) on save/quit. Guarded by X-Internal-Key, not user JWT,
/// because the host reports on behalf of both players in a co-op room.
/// </summary>
[ApiController]
[Route("api/internal/sessions")]
public class InternalSessionController : ControllerBase
{
    private readonly ISessionService _service;
    private readonly IConfiguration _config;

    public InternalSessionController(ISessionService service, IConfiguration config)
    {
        _service = service;
        _config = config;
    }

    private bool KeyValid() => InternalKey.Validate(Request, _config);

    /// <summary>Host creates a new room or re-opens an existing one (by fixed RoomCode).</summary>
    [HttpPost]
    public async Task<IActionResult> CreateOrReopen([FromBody] CreateSessionRequest request)
    {
        if (!KeyValid()) return Unauthorized(ApiResponse.Fail("Valid service authentication is required."));
        var result = await _service.CreateOrReopenAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Host reads full room state (all characters + world state) to load a saved journey.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        if (!KeyValid()) return Unauthorized(ApiResponse.Fail("Valid service authentication is required."));
        var session = await _service.GetDetailAsync(id);
        return session == null
            ? NotFound(ApiResponse.Fail("Room not found."))
            : Ok(ApiResponse<SessionDetailDto>.Ok(session));
    }

    /// <summary>Client looks up a room by the code the host shared, to join it.</summary>
    [HttpGet("by-code/{roomCode}")]
    public async Task<IActionResult> GetByCode(string roomCode)
    {
        if (!KeyValid()) return Unauthorized(ApiResponse.Fail("Valid service authentication is required."));
        var session = await _service.GetByRoomCodeAsync(roomCode);
        return session == null
            ? NotFound(ApiResponse.Fail("Room not found."))
            : Ok(ApiResponse<SessionDetailDto>.Ok(session));
    }

    /// <summary>Host updates room-level metadata (playtime, current scene) on save/quit.</summary>
    [HttpPost("meta")]
    public async Task<IActionResult> UpdateMeta([FromBody] UpdateSessionRequest request)
    {
        if (!KeyValid()) return Unauthorized(ApiResponse.Fail("Valid service authentication is required."));
        var result = await _service.UpdateMetaAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Host upserts one character's progress for a room (keyed on CharacterId + SessionId).</summary>
    [HttpPost("character")]
    public async Task<IActionResult> SaveCharacter([FromBody] SaveCharacterSessionRequest request)
    {
        if (!KeyValid()) return Unauthorized(ApiResponse.Fail("Valid service authentication is required."));
        var result = await _service.SaveCharacterSessionAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Host upserts world/quest progress for a room (keyed on SessionId + EventId).</summary>
    [HttpPost("world-state")]
    public async Task<IActionResult> SaveWorldState([FromBody] SaveWorldStateRequest request)
    {
        if (!KeyValid()) return Unauthorized(ApiResponse.Fail("Valid service authentication is required."));
        var result = await _service.SaveWorldStateAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
