namespace Attrition.API.Controllers;

using Attrition.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[Authorize]
[ApiController]
[Route("api/characters")]
public class CharacterController : ControllerBase
{
    private readonly CharacterService _service;
    public CharacterController(CharacterService service) => _service = service;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetMyCharacters() => Ok(await _service.GetUserCharactersAsync(UserId));

    [HttpPost]
    public async Task<IActionResult> CreateCharacter([FromBody] CreateCharReq req)
    {
        var ch = await _service.CreateCharacterAsync(UserId, req.Name, req.Class);
        return ch != null ? Ok(ch) : BadRequest("Name taken.");
    }
}
public record CreateCharReq(string Name, string Class);