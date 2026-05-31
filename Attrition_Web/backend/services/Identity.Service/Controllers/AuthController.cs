using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Identity.Service.DTOs;
using Identity.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;

namespace Identity.Service.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ICurrentUser _user;
    private readonly IConfiguration _config;
    public AuthController(IAuthService auth, ICurrentUser user, IConfiguration config)
    {
        _auth = auth;
        _user = user;
        _config = config;
    }

    private TimeSpan AccessTtl =>
        TimeSpan.FromMinutes(double.TryParse(_config["Jwt:AccessTokenExpiryMinutes"], out var m) ? m : 15);
    private TimeSpan RefreshTtl =>
        TimeSpan.FromDays(double.TryParse(_config["Jwt:RefreshTokenExpiryDays"], out var d) ? d : 7);

    /// <summary>Sets the auth + CSRF cookies for a web client after a successful auth.</summary>
    private void SetAuthCookies(AuthResponse data)
    {
        var csrf = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        AuthCookies.SetAuth(Response, data.AccessToken, data.RefreshToken, AccessTtl, RefreshTtl, csrf);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var result = await _auth.RegisterAsync(request);
        if (result.Success && result.Data is not null) SetAuthCookies(result.Data);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _auth.LoginAsync(request, ip);
        if (result.Success && result.Data is not null) SetAuthCookies(result.Data);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin(GoogleAuthRequest request)
    {
        var result = await _auth.GoogleLoginAsync(request);
        if (result.Success && result.Data is not null) SetAuthCookies(result.Data);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("google/link")]
    public async Task<IActionResult> LinkGoogle(GoogleAuthRequest request)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _auth.LinkGoogleAsync(userId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("google/unlink")]
    public async Task<IActionResult> UnlinkGoogle()
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _auth.UnlinkGoogleAsync(userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request)
    {
        // Web clients send the refresh token via the HttpOnly cookie (body empty); the game
        // client / API consumers may still post it in the body. Cookie is preferred when present.
        var cookieToken = Request.Cookies[AuthCookies.RefreshToken];
        var effective = !string.IsNullOrEmpty(cookieToken) ? new RefreshRequest(cookieToken) : request;
        var result = await _auth.RefreshAsync(effective);
        if (result.Success && result.Data is not null) SetAuthCookies(result.Data);
        else if (!string.IsNullOrEmpty(cookieToken)) AuthCookies.Clear(Response);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    /// <summary>Issues a fresh CSRF cookie for the SPA to echo via the X-CSRF header.</summary>
    [HttpGet("csrf")]
    public IActionResult Csrf()
    {
        var csrf = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Response.Cookies.Append(AuthCookies.Csrf, csrf, new CookieOptions
        {
            HttpOnly = false,
            Secure = !string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase),
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = RefreshTtl,
        });
        return Ok(ApiResponse<object>.Ok(new { csrf }));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _auth.GetCurrentUserAsync(userId);
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
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _auth.CheckSessionAsync(userId);
        if (!result.Success) return NotFound(result);
        if (result.Data!.IsBanned)
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<SessionStatusDto>.Fail("Account is banned."));
        return Ok(result);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _auth.ChangePasswordAsync(userId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _auth.LogoutAsync(userId);
        AuthCookies.Clear(Response);
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
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _auth.SendVerificationEmailAsync(userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
