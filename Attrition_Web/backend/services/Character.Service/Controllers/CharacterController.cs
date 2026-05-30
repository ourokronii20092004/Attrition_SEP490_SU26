using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Character.Service.DTOs;
using Character.Service.Services;
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
}
