using Attrition.API.DTOs;
using Attrition.API.Models;
using Attrition.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Attrition.API.Controllers;

[Authorize]
[ApiController]
[Route("api/characters")]
public class CharacterController : ControllerBase
{
    private readonly ICharacterService _service;
    public CharacterController(ICharacterService service) => _service = service;

    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<IActionResult> GetMyCharacters() 
        => Ok(new ApiResponse<List<Character>>(true, await _service.GetUserCharactersAsync(UserId)));

    [HttpPost]
    public async Task<IActionResult> CreateCharacter([FromBody] CreateCharReq req)
    {
        var ch = await _service.CreateCharacterAsync(UserId, req.Name, req.Class);
        return ch != null 
            ? Ok(new ApiResponse<Character>(true, ch)) 
            : BadRequest(new ApiResponse(false, "Name taken."));
    }
}