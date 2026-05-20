namespace Attrition.API.Controllers;

using Attrition.API.Models;
using Attrition.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[ApiController]
[Route("api/characters/{charId}/saves")]
public class GameSaveController : ControllerBase
{
    private readonly GameSaveService _service;
    public GameSaveController(GameSaveService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetSaves(Guid charId) => Ok(await _service.GetSavesAsync(charId));

    [HttpPost]
    public async Task<IActionResult> CreateSave(Guid charId, [FromBody] GameSave save)
    {
        save.CharacterId = charId;
        return Ok(await _service.CreateSaveAsync(save));
    }
}