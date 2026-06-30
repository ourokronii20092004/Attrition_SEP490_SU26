using BuildingBlocks.Contracts;
using Enemy.Service.DTOs;
using Enemy.Service.Services;
using Enemy.Service.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enemy.Service.Controllers;

[ApiController]
[Route("api/enemies")]
public class EnemyController : ControllerBase
{
    private readonly IEnemyService _service;
    private readonly IItemService _itemService;
    public EnemyController(IEnemyService service, IItemService itemService)
    {
        _service = service;
        _itemService = itemService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? tier = null, [FromQuery] string? search = null)
    {
        if (tier != null && !EnemyTiers.All.Contains(tier))
            return BadRequest(ApiResponse.Fail($"Invalid tier '{tier}'. Valid tiers: {string.Join(", ", EnemyTiers.All)}."));
        return Ok(ApiResponse<List<EnemyResponse>>.Ok(await _service.GetAllAsync(tier, search)));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var enemy = await _service.GetByIdAsync(id);
        return enemy == null
            ? NotFound(ApiResponse.Fail("Enemy not found."))
            : Ok(ApiResponse<EnemyResponse>.Ok(enemy));
    }

    /// <summary>Game gọi để biết config có đổi không (nhẹ). So với version đã cache để khỏi tải full.</summary>
    [HttpGet("/api/gameconfig/version")]
    public async Task<IActionResult> ConfigVersion()
        => Ok(ApiResponse<GameConfigVersion>.Ok(await _service.GetConfigVersionAsync()));

    /// <summary>Game gọi 1 lần lấy version gộp cả enemy + item, rồi chỉ tải bundle phần nào đổi.</summary>
    [HttpGet("/api/gameconfig/versions")]
    public async Task<IActionResult> ConfigVersions()
    {
        var enemy = await _service.GetConfigVersionAsync();
        var (itemVersion, itemCount) = await _itemService.GetVersionInfoAsync();
        return Ok(ApiResponse<GameConfigVersions>.Ok(
            new GameConfigVersions(enemy.Version, enemy.Count, itemVersion, itemCount)));
    }

    /// <summary>Game tải 1 cục config (enemy + loot) khi version đổi.</summary>
    [HttpGet("/api/gameconfig")]
    public async Task<IActionResult> ConfigBundle()
        => Ok(ApiResponse<GameConfigBundle>.Ok(await _service.GetConfigBundleAsync()));

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
