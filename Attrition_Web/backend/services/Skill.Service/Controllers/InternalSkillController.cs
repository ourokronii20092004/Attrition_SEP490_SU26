using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Microsoft.AspNetCore.Mvc;
using Skill.Service.DTOs;
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

    /// <summary>
    /// Skill search for the global search service. The catalogue is small and already cached whole,
    /// so filtering it in memory avoids adding a query path for a few dozen rows.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 5)
    {
        if (!InternalKey.Validate(Request, config)) return Unauthorized(ApiResponse.Fail("Valid service authentication is required."));
        if (string.IsNullOrWhiteSpace(q)) return Ok(ApiResponse<List<SkillDto>>.Ok(new()));

        var term = q.Trim();
        var all = await service.GetAllAsync();
        var matches = all
            .Where(s => s.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                     || s.SkillId.Contains(term, StringComparison.OrdinalIgnoreCase))
            // Prefix matches first: typing "fire" should surface "Fireball" above "Rapid fire".
            .OrderByDescending(s => s.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            .ThenBy(s => s.Name)
            .Take(Math.Clamp(limit, 1, 50))
            .ToList();
        return Ok(ApiResponse<List<SkillDto>>.Ok(matches));
    }
}
