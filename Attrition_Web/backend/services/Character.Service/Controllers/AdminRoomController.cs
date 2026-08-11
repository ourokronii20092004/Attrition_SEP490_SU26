using BuildingBlocks.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Character.Service.Controllers;

/// <summary>
/// Admin view over co-op rooms: who played with whom, in which role, and how far the room's shared
/// world has progressed. The data was already being written; nothing exposed it before.
/// </summary>
[ApiController]
[Route("api/admin/rooms")]
[Authorize(Roles = Roles.Admin)]
public class AdminRoomController : ControllerBase
{
    private readonly ISessionService _service;

    public AdminRoomController(ISessionService service) => _service = service;

    /// <summary>Paged rooms, most recently played first.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetRoomsForAdminAsync(page, pageSize, ct);
        return Ok(ApiResponse<DTOs.AdminRoomListDto>.Ok(result));
    }

    /// <summary>One room in full: its party, its shared world state, and how that state changed.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
    {
        var result = await _service.GetRoomDetailForAdminAsync(id, ct);
        return result == null
            ? NotFound(ApiResponse<DTOs.AdminRoomDetailDto>.Fail("Room not found."))
            : Ok(ApiResponse<DTOs.AdminRoomDetailDto>.Ok(result));
    }
}