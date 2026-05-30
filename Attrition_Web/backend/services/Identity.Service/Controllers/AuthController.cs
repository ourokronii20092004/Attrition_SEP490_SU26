using BuildingBlocks.Contracts;
using Identity.Service.DTOs;
using Identity.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Service.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    private Guid CurrentUserId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var result = await _auth.RegisterAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _auth.LoginAsync(request, ip);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin(GoogleAuthRequest request)
    {
        var result = await _auth.GoogleLoginAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("google/link")]
    public async Task<IActionResult> LinkGoogle(GoogleAuthRequest request)
    {
        var result = await _auth.LinkGoogleAsync(CurrentUserId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("google/unlink")]
    public async Task<IActionResult> UnlinkGoogle()
    {
        var result = await _auth.UnlinkGoogleAsync(CurrentUserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request)
    {
        var result = await _auth.RefreshAsync(request);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var result = await _auth.GetCurrentUserAsync(CurrentUserId);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Lightweight liveness/ban check the game client polls (~every 10s). Returns 403 when the
    /// account is banned so the client can kick the player out of the running game session.
    /// </summary>
    [Authorize]
    [HttpGet("session-check")]
    public async Task<IActionResult> SessionCheck()
    {
        var result = await _auth.CheckSessionAsync(CurrentUserId);
        if (!result.Success) return NotFound(result);
        if (result.Data!.IsBanned)
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<SessionStatusDto>.Fail("Account is banned."));
        return Ok(result);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var result = await _auth.ChangePasswordAsync(CurrentUserId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var result = await _auth.LogoutAsync(CurrentUserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var result = await _auth.ForgotPasswordAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var result = await _auth.ResetPasswordAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request)
    {
        var result = await _auth.VerifyEmailAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("verify-email/resend")]
    public async Task<IActionResult> ResendVerification()
    {
        var result = await _auth.SendVerificationEmailAsync(CurrentUserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
