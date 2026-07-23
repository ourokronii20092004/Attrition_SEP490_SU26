using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Microsoft.AspNetCore.Mvc;
using Skill.Service.Services;

namespace Skill.Service.Controllers;

[ApiController]
[Route("api/internal/skills")]
public class InternalSkillController(ISkillService service, IConfiguration config) : ControllerBase
{
    [HttpGet("count")]
    public async Task<IActionResult> Count()
    {
        if (!InternalKey.Validate(Request, config)) return Unauthorized(ApiResponse.Fail("Valid service authentication is required."));
        return Ok(ApiResponse<int>.Ok(await service.CountAsync()));
    }
}
