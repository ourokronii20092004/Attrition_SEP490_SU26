using BuildingBlocks.Contracts;
using Enemy.Service.DTOs;
using Enemy.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enemy.Service.Controllers;

[ApiController]
[Route("api/skills")]
public class SkillController : ControllerBase
{
    private readonly ISkillService _service;
    public SkillController(ISkillService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(ApiResponse<List<SkillResponse>>.Ok(await _service.GetAllAsync()));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var skill = await _service.GetByIdAsync(id);
        return skill == null ? NotFound(ApiResponse.Fail("Skill not found.")) : Ok(ApiResponse<SkillResponse>.Ok(skill));
    }

    [HttpGet("/api/skillconfig")]
    public async Task<IActionResult> ConfigBundle() =>
        Ok(ApiResponse<SkillConfigBundle>.Ok(await _service.GetConfigBundleAsync()));

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] SkillUpdateRequest request)
    {
        if (!string.Equals(id, request.SkillId, StringComparison.Ordinal))
            return BadRequest(ApiResponse.Fail("Route ID must match SkillId."));
        var result = await _service.UpdateAsync(id, request);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
