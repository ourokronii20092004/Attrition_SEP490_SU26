using BuildingBlocks.Contracts;
using Enemy.Service.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enemy.Service.Controllers;

[ApiController]
[Route("api/items")]
public class ItemController : ControllerBase
{
    private readonly IItemService _service;

    public ItemController(IItemService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? category = null, [FromQuery] string? search = null)
        => Ok(ApiResponse<List<ItemResponse>>.Ok(await _service.GetAllAsync(category, search)));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var item = await _service.GetByIdAsync(id);
        return item == null
            ? NotFound(ApiResponse.Fail("Item not found."))
            : Ok(ApiResponse<ItemResponse>.Ok(item));
    }

    /// <summary>Game tải cục item config (item + modifiers) khi version đổi.</summary>
    [HttpGet("/api/itemconfig")]
    public async Task<IActionResult> ConfigBundle()
        => Ok(ApiResponse<ItemConfigBundle>.Ok(await _service.GetConfigBundleAsync()));

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ItemCreateRequest request)
    {
        var result = await _service.CreateAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] ItemUpdateRequest request)
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