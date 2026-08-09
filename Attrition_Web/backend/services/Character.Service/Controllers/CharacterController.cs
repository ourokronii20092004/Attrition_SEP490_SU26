using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Character.Service.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Character.Service.Controllers;

/// <summary>Player-facing character status (read-only). A user sees only their own characters.</summary>
[ApiController]
[Route("api/characters")]
[Authorize]
public class CharacterController : ControllerBase
{
    private readonly ICharacterService _service;
    private readonly ICurrentUser _user;

    public CharacterController(ICharacterService service, ICurrentUser user)
    {
        _service = service;
        _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        return Ok(ApiResponse<List<CharacterSummaryDto>>.Ok(await _service.GetByOwnerAsync(userId)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var character = await _service.GetDetailAsync(id);
        if (character == null) return NotFound(ApiResponse.Fail("Character not found."));
        // Ownership guard: a player may only read their own; admins may read any.
        if (character.OwnerId != userId && !_user.IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("You do not have access to this character."));
        return Ok(ApiResponse<CharacterDetailDto>.Ok(character));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var response = await _service.DeleteAsync(id, userId, _user.IsAdmin);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    /// <summary>
    /// The game client posts a character status snapshot on save/quit. Authenticated with the
    /// player's JWT — the owner is taken from the token, so a caller can only snapshot their own
    /// character (host snapshots itself; client progress lives in character_session, not here).
    /// </summary>
    [HttpPost("snapshot")]
    public async Task<IActionResult> Snapshot([FromBody] SnapshotIngestRequest request)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _service.IngestSnapshotAsync(request with { OwnerId = userId });
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Save files ───────────────────────────────────────────────────────────────────────────

    /// <summary>Paged save history for a character, newest first.</summary>
    [HttpGet("{id:guid}/saves")]
    public async Task<IActionResult> GetSaves(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _service.GetSavesAsync(id, userId, _user.IsAdmin, page, pageSize);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>One save in full — every number as it was at that moment.</summary>
    [HttpGet("{id:guid}/saves/{saveId:long}")]
    public async Task<IActionResult> GetSave(Guid id, long saveId)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _service.GetSaveAsync(id, saveId, userId, _user.IsAdmin);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Delete a save. Deleting the newest rolls live game state back to the previous save; the body
    /// may additionally ask to roll the room's shared world state back, which is honoured only for
    /// the room's owner.
    /// </summary>
    [HttpDelete("{id:guid}/saves/{saveId:long}")]
    public async Task<IActionResult> DeleteSave(Guid id, long saveId, [FromBody] DeleteSaveRequest? request = null)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _service.DeleteSaveAsync(
            id, saveId, userId, _user.IsAdmin, request?.AlsoRollBackWorldState ?? false);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}