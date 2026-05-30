using BuildingBlocks.Contracts;
using Identity.Service.DTOs;
using Identity.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Service.Controllers;

[ApiController]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly IAccountService _account;
    public AccountController(IAccountService account) => _account = account;

    private Guid CurrentUserId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet("profile/{username}")]
    public async Task<IActionResult> GetProfile(string username)
    {
        var result = await _account.GetProfileByUsernameAsync(username);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        var result = await _account.UpdateProfileAsync(CurrentUserId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPut("theme")]
    public async Task<IActionResult> UpdateTheme(UpdateThemeRequest request)
    {
        var result = await _account.UpdateThemeAsync(CurrentUserId, request.ThemeMode, request.ThemeAccent);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [Authorize]
    [HttpPost("avatar")]
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        var result = await _account.UpdateAvatarAsync(CurrentUserId, file);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpDelete("avatar")]
    public async Task<IActionResult> DeleteAvatar()
    {
        await _account.DeleteAvatarAsync(CurrentUserId);
        return Ok(ApiResponse.Ok());
    }

    [Authorize]
    [HttpPost("background")]
    public async Task<IActionResult> UploadBackground(IFormFile file)
    {
        var result = await _account.UpdateBackgroundAsync(CurrentUserId, file);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpDelete("background")]
    public async Task<IActionResult> DeleteBackground()
    {
        await _account.DeleteBackgroundAsync(CurrentUserId);
        return Ok(ApiResponse.Ok());
    }

    [Authorize]
    [HttpPost("set-password")]
    public async Task<IActionResult> SetPassword(SetPasswordRequest request)
    {
        var result = await _account.SetPasswordAsync(CurrentUserId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPut("email")]
    public async Task<IActionResult> UpdateEmail(UpdateEmailRequest request)
    {
        var result = await _account.UpdateEmailAsync(CurrentUserId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpDelete("me")]
    public async Task<IActionResult> DeleteAccount()
    {
        var result = await _account.DeleteAccountAsync(CurrentUserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
