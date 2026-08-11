using BuildingBlocks.Contracts;
using Enemy.Service.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enemy.Service.Controllers;

[ApiController]
[Route("api/admin/game-data")]
[Authorize(Roles = Roles.Admin)]
public class GameDataImportController : ControllerBase
{
    private readonly IGameDataImportService _service;

    public GameDataImportController(IGameDataImportService service) => _service = service;

    [HttpPost("import")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Import([FromBody] GameDataImportRequest request)
    {
        var result = await _service.ImportAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}