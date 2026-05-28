using Attrition.API.DTOs;
using System.Security.Claims;
using Attrition.API.Models;
using Attrition.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attrition.API.Controllers;

[Authorize]
[ApiController]
[Route("api/characters/{charId}/saves")]
public class GameSaveController : ControllerBase
{
    private readonly IGameSaveService _service;
    public GameSaveController(IGameSaveService service) => _service = service;
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<IActionResult> GetSaves(Guid charId) 
        => Ok(new ApiResponse<List<GameSave>>(true, await _service.GetSavesAsync(charId, UserId)));

    [HttpPost]
    public async Task<IActionResult> CreateSave(Guid charId, [FromBody] GameSave save)
    {
        save.CharacterId = charId;
        return Ok(new ApiResponse<GameSave>(true, await _service.CreateSaveAsync(save, UserId)));
    }

    [HttpDelete("{saveId}")]
    public async Task<IActionResult> DeleteSave(Guid charId, Guid saveId)
    {
        var result = await _service.DeleteSaveAsync(saveId, UserId);
        if (!result) return NotFound(new ApiResponse(false, "Save file not found."));
        return Ok(new ApiResponse(true));
    }

    [HttpPut("{saveId}")]
    public async Task<IActionResult> RenameSave(Guid charId, Guid saveId, [FromBody] RenameSaveRequest request)
    {
        var result = await _service.RenameSaveAsync(saveId, request.NewName, UserId);
        if (result == null) return NotFound(new ApiResponse<GameSave>(false, Error: "Save file not found."));
        return Ok(new ApiResponse<GameSave>(true, result));
    }
}