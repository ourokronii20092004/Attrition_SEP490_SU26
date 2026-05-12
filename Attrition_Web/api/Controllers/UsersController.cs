using Attrition.API.DTOs;
using Attrition.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attrition.API.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly FileService _files;
    public UsersController(AuthService auth, FileService files) { _auth = auth; _files = files; }

    [HttpGet("{username}/profile")]
    public async Task<IActionResult> GetProfile(string username)
    {
        var result = await _auth.GetProfileByUsernameAsync(username);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _auth.UpdateProfileAsync(userId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("avatar")]
    public async Task<IActionResult> UploadAvatar(IFormFile file, [FromServices] Attrition.API.Data.AppDbContext db)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _files.UploadAvatarAsync(userId, file);
        if (result.Success)
        {
            var user = await db.Users.FindAsync(userId);
            if (user != null)
            {
                user.AvatarPath = result.Data;
                await db.SaveChangesAsync();
            }
        }
        return result.Success ? Ok(result) : BadRequest(result);
    }
}