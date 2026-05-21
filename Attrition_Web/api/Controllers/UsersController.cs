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
    private readonly UserService _users;

    public UsersController(AuthService auth, FileService files, UserService users)
    {
        _auth = auth;
        _files = files;
        _users = users;
    }

    [HttpGet("{username}/profile")]
    public async Task<IActionResult> GetProfile(string username)
    {
        var result = await _auth.GetProfileByUsernameAsync(username);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("{username}/posts")]
    public async Task<IActionResult> GetUserPosts(string username, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _users.GetUserPostsAsync(username, page, pageSize);
        if (result == null) return NotFound(new ApiResponse(false, "User not found."));
        return Ok(result);
    }

    [HttpGet("{username}/contributions")]
    public async Task<IActionResult> GetUserContributions(string username, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _users.GetUserContributionsAsync(username, page, pageSize);
        if (result == null) return NotFound(new ApiResponse(false, "User not found."));
        return Ok(result);
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
    [HttpPut("theme")]
    public async Task<IActionResult> UpdateTheme(UpdateThemeRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _users.UpdateThemeAsync(userId, request.ThemeMode, request.ThemeAccent);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [Authorize]
    [HttpPost("avatar")]
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _files.UploadAvatarAsync(userId, file);
        if (result.Success)
        {
            await _users.UpdateAvatarAsync(userId, result.Data);
        }
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpDelete("avatar")]
    public async Task<IActionResult> DeleteAvatar()
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        await _users.DeleteAvatarAsync(userId);
        return Ok(new ApiResponse(true));
    }

    [Authorize]
    [HttpPost("background")]
    public async Task<IActionResult> UploadBackground(IFormFile file)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _files.UploadBackgroundAsync(userId, file);
        if (result.Success)
        {
            await _users.UpdateBackgroundAsync(userId, result.Data);
        }
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpDelete("background")]
    public async Task<IActionResult> DeleteBackground()
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        await _users.DeleteBackgroundAsync(userId);
        return Ok(new ApiResponse(true));
    }

    [Authorize]
    [HttpPost("upload/image")]
    public async Task<IActionResult> UploadContentImage(IFormFile file)
    {
        var result = await _files.UploadContentImageAsync(file);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}