using Attrition.API.DTOs;
using Attrition.API.Models;
using Attrition.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Attrition.API.Controllers;

[Authorize]
[ApiController]
[Route("api/rooms")]
public class RoomController : ControllerBase
{
    private readonly IRoomService _service;
    public RoomController(IRoomService service) => _service = service;
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);

    [HttpPost]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomReq req) 
        => Ok(new ApiResponse<GameRoom>(true, await _service.CreateRoomAsync(UserId, req.RoomName, req.IsPrivate)));

    [HttpGet]
    public async Task<IActionResult> GetPublicRooms()
    {
        var rooms = await _service.GetPublicRoomsAsync();
        return Ok(new ApiResponse<List<GameRoom>>(true, rooms));
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetRoom(string code)
    {
        var room = await _service.GetRoomByCodeAsync(code);
        if (room == null) return NotFound(new ApiResponse(false, "Room not found"));
        return Ok(new ApiResponse<GameRoom>(true, room));
    }

    [HttpPost("{code}/join")]
    public async Task<IActionResult> JoinRoom(string code, [FromBody] JoinRoomReq req)
    {
        var room = await _service.GetRoomByCodeAsync(code);
        if (room == null) return NotFound(new ApiResponse(false, "Room not found"));
        var success = await _service.JoinRoomAsync(room.RoomId, UserId, req.CharacterId);
        return success ? Ok(new ApiResponse(true)) : BadRequest(new ApiResponse(false, "Failed to join room. Room might be full or ended."));
    }

    [HttpPost("{code}/leave")]
    public async Task<IActionResult> LeaveRoom(string code)
    {
        var room = await _service.GetRoomByCodeAsync(code);
        if (room == null) return NotFound(new ApiResponse(false, "Room not found"));
        var success = await _service.LeaveRoomAsync(room.RoomId, UserId);
        return success ? Ok(new ApiResponse(true)) : BadRequest(new ApiResponse(false, "Failed to leave room"));
    }

    [HttpPost("{code}/ready")]
    public async Task<IActionResult> ToggleReady(string code)
    {
        var room = await _service.GetRoomByCodeAsync(code);
        if (room == null) return NotFound(new ApiResponse(false, "Room not found"));
        var success = await _service.ToggleReadyAsync(room.RoomId, UserId);
        return success ? Ok(new ApiResponse(true)) : BadRequest(new ApiResponse(false, "Failed to toggle ready state"));
    }

    [HttpPost("{code}/start")]
    public async Task<IActionResult> StartRoom(string code)
    {
        var room = await _service.GetRoomByCodeAsync(code);
        if (room == null) return NotFound(new ApiResponse(false, "Room not found"));
        var success = await _service.StartRoomAsync(room.RoomId, UserId);
        return success ? Ok(new ApiResponse(true)) : BadRequest(new ApiResponse(false, "Failed to start room. Only host can start waiting room."));
    }

    [HttpPost("{code}/end")]
    public async Task<IActionResult> EndRoom(string code)
    {
        var room = await _service.GetRoomByCodeAsync(code);
        if (room == null) return NotFound(new ApiResponse(false, "Room not found"));
        var success = await _service.EndRoomAsync(room.RoomId, UserId);
        return success ? Ok(new ApiResponse(true)) : BadRequest(new ApiResponse(false, "Failed to end room. Only host can end room."));
    }
}