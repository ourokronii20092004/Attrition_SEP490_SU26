using BuildingBlocks.Authentication;
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
    private readonly ICurrentUser _user;
    public AccountController(IAccountService account, ICurrentUser user)
    {
        _account = account;
        _user = user;
    }

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
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _account.UpdateProfileAsync(userId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPut("theme")]
    public async Task<IActionResult> UpdateTheme(UpdateThemeRequest request)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _account.UpdateThemeAsync(userId, request.ThemeMode, request.ThemeAccent);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [Authorize]
    [HttpPost("avatar")]
    public async Task<IActionResult> UploadAvatar(IFormFile? file)
    {
        if (file is null) return BadRequest(ApiResponse.Fail("No file was provided."));
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _account.UpdateAvatarAsync(userId, file);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpDelete("avatar")]
    public async Task<IActionResult> DeleteAvatar()
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        await _account.DeleteAvatarAsync(userId);
        return Ok(ApiResponse.Ok());
    }

    [Authorize]
    [HttpPost("background")]
    public async Task<IActionResult> UploadBackground(IFormFile? file)
    {
        if (file is null) return BadRequest(ApiResponse.Fail("No file was provided."));
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _account.UpdateBackgroundAsync(userId, file);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpDelete("background")]
    public async Task<IActionResult> DeleteBackground()
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        await _account.DeleteBackgroundAsync(userId);
        return Ok(ApiResponse.Ok());
    }

    [Authorize]
    [HttpPost("set-password")]
    public async Task<IActionResult> SetPassword(SetPasswordRequest request)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _account.SetPasswordAsync(userId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPut("email")]
    public async Task<IActionResult> UpdateEmail(UpdateEmailRequest request)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _account.UpdateEmailAsync(userId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpDelete("me")]
    public async Task<IActionResult> DeleteAccount()
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _account.DeleteAccountAsync(userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
