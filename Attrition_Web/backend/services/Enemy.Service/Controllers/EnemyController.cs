using BuildingBlocks.Contracts;
using Enemy.Service.DTOs;
using Enemy.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enemy.Service.Controllers;

[ApiController]
[Route("api/enemies")]
public class EnemyController : ControllerBase
{
    private readonly IEnemyService _service;
    public EnemyController(IEnemyService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? tier = null, [FromQuery] string? search = null)
        => Ok(ApiResponse<List<EnemyResponse>>.Ok(await _service.GetAllAsync(tier, search)));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var enemy = await _service.GetByIdAsync(id);
        return enemy == null
            ? NotFound(ApiResponse.Fail("Enemy not found."))
            : Ok(ApiResponse<EnemyResponse>.Ok(enemy));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EnemyCreateRequest request)
    {
        var result = await _service.CreateAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] EnemyUpdateRequest request)
    {
        var result = await _service.UpdateAsync(id, request);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _service.DeleteAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
