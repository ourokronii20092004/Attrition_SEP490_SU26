using BuildingBlocks.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skill.Service.DTOs;

namespace Skill.Service.Controllers;

[ApiController]
[Route("api/admin/skill-data")]
[Authorize(Roles = Roles.Admin)]
public class SkillImportController(ISkillService service) : ControllerBase
{
    [HttpPost("import")]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> Import(SkillImportRequest request)
    {
        var result = await service.ImportAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}