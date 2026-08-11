using BuildingBlocks.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skill.Service.DTOs;

namespace Skill.Service.Controllers;

[ApiController]
[Route("api/skills")]
public class SkillController(ISkillService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(ApiResponse<List<SkillDto>>.Ok(await service.GetAllAsync()));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var result = await service.GetByIdAsync(id);
        return result == null ? NotFound(ApiResponse.Fail("Skill not found.")) : Ok(ApiResponse<SkillDto>.Ok(result));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, SkillUpdateRequest request)
    {
        var result = await service.UpdateAsync(id, request);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await service.DeleteAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("/api/skillconfig")]
    public async Task<IActionResult> Config() => Ok(ApiResponse<SkillConfigBundle>.Ok(await service.GetConfigBundleAsync()));
}