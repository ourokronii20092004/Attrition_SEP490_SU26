namespace Attrition.API.Controllers;

using Attrition.API.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/game/data")]
public class GameDataController : ControllerBase
{
    private readonly GameDataService _service;
    public GameDataController(GameDataService service) => _service = service;

    [HttpGet("items")]
    public async Task<IActionResult> GetItems() => Ok(await _service.GetItemsAsync());

    [HttpGet("enemies")]
    public async Task<IActionResult> GetEnemies() => Ok(await _service.GetEnemiesAsync());

    [HttpGet("skills")]
    public async Task<IActionResult> GetSkills() => Ok(await _service.GetSkillsAsync());

    [HttpGet("levels")]
    public async Task<IActionResult> GetLevels() => Ok(await _service.GetLevelsAsync());
}