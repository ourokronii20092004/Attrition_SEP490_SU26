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

    private Guid UserId => _user.UserId!.Value;

    [HttpGet]
    public async Task<IActionResult> GetMine()
        => Ok(ApiResponse<List<CharacterSummaryDto>>.Ok(await _service.GetByOwnerAsync(UserId)));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var character = await _service.GetDetailAsync(id);
        if (character == null) return NotFound(ApiResponse.Fail("Character not found."));
        // Ownership guard: a player may only read their own; admins may read any.
        if (character.OwnerId != UserId && !_user.IsAdmin)
            return Forbid();
        return Ok(ApiResponse<CharacterDetailDto>.Ok(character));
    }
}
