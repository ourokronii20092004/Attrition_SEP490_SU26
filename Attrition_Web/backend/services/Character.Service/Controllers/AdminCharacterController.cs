using BuildingBlocks.Contracts;
using Character.Service.DTOs;
using Character.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Character.Service.Controllers;

/// <summary>Admin view of every player's characters (read-only).</summary>
[ApiController]
[Route("api/admin/characters")]
[Authorize(Roles = Roles.Admin)]
public class AdminCharacterController : ControllerBase
{
    private readonly ICharacterService _service;
    public AdminCharacterController(ICharacterService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(ApiResponse<PaginatedResponse<AdminCharacterDto>>.Ok(
            await _service.GetAllAsync(page < 1 ? 1 : page, pageSize, ct)));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var character = await _service.GetDetailAsync(id);
        return character == null
            ? NotFound(ApiResponse.Fail("Character not found."))
            : Ok(ApiResponse<CharacterDetailDto>.Ok(character));
    }
}
