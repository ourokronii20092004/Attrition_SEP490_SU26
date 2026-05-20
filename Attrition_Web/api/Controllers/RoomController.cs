namespace Attrition.API.Controllers;

using Attrition.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[Authorize]
[ApiController]
[Route("api/rooms")]
public class RoomController : ControllerBase
{
    private readonly RoomService _service;
    public RoomController(RoomService service) => _service = service;
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<IActionResult> CreateRoom() => Ok(await _service.CreateRoomAsync(UserId));

    [HttpGet("{code}")]
    public async Task<IActionResult> GetRoom(string code) => Ok(await _service.GetRoomByCodeAsync(code));

    [HttpPost("{code}/join")]
    public async Task<IActionResult> JoinRoom(string code, [FromBody] JoinRoomReq req)
    {
        var room = await _service.GetRoomByCodeAsync(code);
        if (room == null) return NotFound();
        var success = await _service.JoinRoomAsync(room.RoomId, UserId, req.CharacterId);
        return success ? Ok() : BadRequest();
    }
}
public record JoinRoomReq(Guid CharacterId);