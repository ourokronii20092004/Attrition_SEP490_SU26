using Attrition.API.DTOs;
using Attrition.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Attrition.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/rooms/{code}")]
public class RoomCommandController : ControllerBase
{
    private readonly IGameSessionService _sessionService;

    public RoomCommandController(IGameSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [HttpGet("state")]
    public async Task<IActionResult> GetState(string code)
    {
        var state = await _sessionService.GetRoomStateAsync(code);
        if (state == null)
        {
            return NotFound(new ApiResponse(false, "No active session state found in Redis."));
        }
        return Ok(new ApiResponse<string>(true, state));
    }

    [HttpPut("state")]
    public async Task<IActionResult> UpdateState(string code, [FromBody] string stateJson)
    {
        await _sessionService.SaveRoomStateAsync(code, stateJson);
        return Ok(new ApiResponse(true, "State updated successfully."));
    }

    [HttpPut("commands")]
    public async Task<IActionResult> DispatchCommand(string code, [FromBody] DispatchCommandReq req)
    {
        await _sessionService.PublishCommandAsync(code, req.Command, req.Payload);
        return Ok(new ApiResponse(true, "Command published successfully."));
    }
}

