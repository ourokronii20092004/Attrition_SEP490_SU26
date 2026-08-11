using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Identity.Service.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

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

    /// <summary>Sets the auth + CSRF cookies for a web client after a successful auth. When
    /// <paramref name="persistent"/> is false ("remember me" off) they are session cookies.</summary>
    private void SetAuthCookies(AuthResponse data, bool persistent)
    {
        var csrf = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        AuthCookies.SetAuth(Response, data.AccessToken, data.RefreshToken, AccessTtl, RefreshTtl, csrf, persistent);
    }

    /// <summary>The persistence the user last chose, read from the remember marker so token
    /// refreshes keep session logins as session cookies. Defaults to persistent when unknown.</summary>
    private bool RememberedPersistent() => Request.Cookies[AuthCookies.Remember] != "0";

    private const string GoogleStateCookie = "google_oauth_state";

    private static bool IsProd =>
        !string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "Development", StringComparison.OrdinalIgnoreCase);

    private string ClientUrl() => (_config["App:ClientUrl"] ?? "http://localhost:3000").TrimEnd('/');

    /// <summary>
    /// The OAuth redirect URI Google calls back into. Prefer the explicit config value (required in
    /// dev, where the API and web live on different origins); otherwise derive it from the client
    /// URL, which is correct in prod where the API is same-origin as the web app. Whatever this
    /// resolves to MUST be registered under the OAuth client's "Authorized redirect URIs" in the
    /// Google Cloud console.
    /// </summary>
    private string GoogleRedirectUri()
    {
        var configured = _config["Authentication:Google:RedirectUri"];
        return !string.IsNullOrWhiteSpace(configured)
            ? configured
            : $"{ClientUrl()}/api/auth/google/callback";
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var result = await _auth.RegisterAsync(request);
        // Intentionally NO SetAuthCookies here: a new account must verify its email before it can
        // sign in (see the gate in LoginAsync). The client sends the user to the "verify your email"
        // screen instead of logging them straight in. (Tokens still ride in the body for the game
        // client, which manages its own session.)
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Resolve the real client IP. The ingress chain is Cloudflare → nginx → gateway → here, so
    /// HttpContext.Connection.RemoteIpAddress is the proxy's container IP. Cloudflare sets
    /// CF-Connecting-IP with the true client address; nginx/gateway forward it. Fall back to the
    /// first X-Forwarded-For hop, then the raw connection IP.
    /// </summary>
    private string? ClientIp()
    {
        var cf = Request.Headers["CF-Connecting-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(cf)) return cf.Trim();
        var xff = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(xff)) return xff.Split(',')[0].Trim();
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var ip = ClientIp();
        var result = await _auth.LoginAsync(request, ip);
        if (result.Success && result.Data is not null) SetAuthCookies(result.Data, request.RememberMe);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin(GoogleAuthRequest request)
    {
        var result = await _auth.GoogleLoginAsync(request, ClientIp());
        if (result.Success && result.Data is not null) SetAuthCookies(result.Data, true);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Begins the popup-free Google sign-in: a top-level redirect to Google's account chooser.
    /// A random nonce is stored in a short-lived cookie and echoed via <c>state</c> for CSRF
    /// protection; the "unity" client flag rides along so the callback knows where to send the user.
    /// </summary>
    [HttpGet("google/start")]
    public IActionResult GoogleStart([FromQuery] string? client, [FromQuery] string? mode)
    {
        var isLink = string.Equals(mode, "link", StringComparison.OrdinalIgnoreCase);
        var clientId = _config["Authentication:Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            return Redirect(isLink
                ? $"{ClientUrl()}/settings?link_error={Uri.EscapeDataString("Google sign-in isn't configured.")}"
                : $"{ClientUrl()}/login?auth_error=google_unconfigured");

        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var isUnity = string.Equals(client, "unity", StringComparison.OrdinalIgnoreCase);
        // Intent rides in state (nonce.intent) so the shared callback knows whether to log in or link.
        var intent = isLink ? "link" : isUnity ? "unity" : "web";
        var state = $"{nonce}.{intent}";

        // Lax (not Strict): the callback is a top-level GET navigation coming FROM accounts.google.com,
        // so a Strict cookie would be withheld. HttpOnly + a short TTL keep the nonce locked down.
        Response.Cookies.Append(GoogleStateCookie, nonce, new CookieOptions
        {
            HttpOnly = true,
            Secure = IsProd,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth/google",
            MaxAge = TimeSpan.FromMinutes(10),
        });

        // prompt=select_account gives the "click your account" chooser the user expects.
        var authUrl = "https://accounts.google.com/o/oauth2/v2/auth"
            + $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(GoogleRedirectUri())}"
            + "&response_type=code"
            + $"&scope={Uri.EscapeDataString("openid email profile")}"
            + "&access_type=online"
            + "&prompt=select_account"
            + $"&state={Uri.EscapeDataString(state)}";
        return Redirect(authUrl);
    }

    /// <summary>
    /// Google's redirect target. Verifies the CSRF state, exchanges the authorization code for
    /// tokens, sets the auth cookies, then bounces the browser back to the web app — or to the local
    /// game client when the flow was started with <c>?client=unity</c>. Failures redirect to
    /// /login?auth_error=… so the sign-in page can show a message.
    /// </summary>
    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
    {
        var cookieNonce = Request.Cookies[GoogleStateCookie];
        Response.Cookies.Delete(GoogleStateCookie, new CookieOptions { Path = "/api/auth/google" });

        var parts = (state ?? string.Empty).Split('.', 2);
        var stateNonce = parts.Length > 0 ? parts[0] : string.Empty;
        var intent = parts.Length > 1 ? parts[1] : "web";
        var isUnity = intent == "unity";
        var isLink = intent == "link";

        // Link failures return to /settings with a readable message; login failures to /login with a code.
        string Fail(string reason) => isLink
            ? $"{ClientUrl()}/settings?link_error={Uri.EscapeDataString(reason)}"
            : $"{ClientUrl()}/login?auth_error={reason}{(isUnity ? "&client=unity" : string.Empty)}";

        // User dismissed Google's consent/account screen.
        if (!string.IsNullOrEmpty(error))
            return Redirect(Fail(isLink ? "Google connection was cancelled." : "google_denied"));

        // CSRF: the nonce echoed back in state must match the one we stored in the cookie.
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(stateNonce) || string.IsNullOrEmpty(cookieNonce)
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stateNonce), Encoding.UTF8.GetBytes(cookieNonce)))
            return Redirect(Fail(isLink ? "Your session expired. Please try connecting again." : "google_state"));

        // Link intent: attach Google to the CURRENT user, read from the Lax auth cookie (sent on this
        // top-level callback). No user id is trusted from `state` — that would allow linking to an
        // arbitrary account.
        if (isLink)
        {
            var uid = _user.UserId;
            if (uid is null)
                return Redirect(Fail("You need to be signed in to connect Google."));
            var linkRes = await _auth.LinkGoogleByCodeAsync(uid.Value, code, GoogleRedirectUri());
            return linkRes.Success
                ? Redirect($"{ClientUrl()}/settings?linked=1")
                : Redirect(Fail(linkRes.Error ?? "Could not connect Google."));
        }

        var result = await _auth.GoogleCodeExchangeAsync(code, GoogleRedirectUri(), ClientIp());
        if (!result.Success || result.Data is null)
            return Redirect(Fail("google_failed"));

        SetAuthCookies(result.Data, true);

        if (isUnity)
        {
            // Hand the tokens to the player's local game host (loopback), matching the email/password
            // game-login handoff so a Google login persists the session the same way.
            var gameUrl = (_config["App:GameClientUrl"] ?? "http://localhost:52000").TrimEnd('/');
            return Redirect($"{gameUrl}/?token={Uri.EscapeDataString(result.Data.AccessToken)}"
                + $"&refresh={Uri.EscapeDataString(result.Data.RefreshToken)}");
        }
        return Redirect($"{ClientUrl()}/");
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
        if (result.Success && result.Data is not null) SetAuthCookies(result.Data, RememberedPersistent());
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

        // Revoked session? A password change / reset bumps TokensValidAfter; any token minted before
        // it (its "sat" claim) is dead. 401 so the SPA's session poll signs this device out.
        if (result.Data.TokensValidAfter is { } cutoff)
        {
            var satClaim = User.FindFirst("sat")?.Value;
            if (long.TryParse(satClaim, out var sat)
                && sat < new DateTimeOffset(DateTime.SpecifyKind(cutoff, DateTimeKind.Utc)).ToUnixTimeSeconds())
                return Unauthorized(ApiResponse<SessionStatusDto>.Fail("Your session has ended. Please sign in again."));
        }
        return Ok(result);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _auth.ChangePasswordAsync(userId, request);
        // Swap in the freshly-minted session cookies so this device stays signed in after the change
        // (other devices are invalidated server-side), preserving the chosen "remember me" persistence.
        if (result.Success && result.Data is not null) SetAuthCookies(result.Data, RememberedPersistent());
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