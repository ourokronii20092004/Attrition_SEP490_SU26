using System.Text.Json;
using BuildingBlocks.Contracts;
using Google.Apis.Auth;
using Identity.Service.DTOs;
using Identity.Service.Models;
using Identity.Service.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Identity.Service.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IConfiguration _config;
    private readonly IEmailService _email;
    private readonly TokenService _tokens;
    private readonly ILogger<AuthService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthService(IUserRepository userRepo, IConfiguration config, IEmailService email,
        TokenService tokens, ILogger<AuthService> logger, IHttpClientFactory httpClientFactory)
    {
        _userRepo = userRepo;
        _config = config;
        _email = email;
        _tokens = tokens;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request)
    {
        // Single generic message for both username and email clashes so registration can't be
        // used to enumerate which usernames/emails already exist.
        const string takenMessage = "That username or email is already in use.";

        // Normalize before storing: usernames are lowercase (case-insensitive lookups already
        // collapse case, so this keeps the stored/displayed value consistent) and both fields are
        // trimmed so stray whitespace can't create "different" accounts.
        var username = request.Username.Trim().ToLowerInvariant();
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();

        if (!await _userRepo.IsUsernameAvailableAsync(username))
            return ApiResponse<AuthResponse>.Fail(takenMessage);

        if (!string.IsNullOrEmpty(email) && await _userRepo.GetByEmailAsync(email) != null)
            return ApiResponse<AuthResponse>.Fail(takenMessage);

        var verifyToken = TokenService.NewRawToken();
        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsEmailVerified = false,
            EmailVerification = new() { Token = TokenService.HashToken(verifyToken), ExpiresAt = DateTime.UtcNow.AddHours(24) }
        };

        var (accessToken, refreshToken) = _tokens.GenerateTokens(user);
        user.Refresh = new() { Token = TokenService.HashToken(refreshToken), ExpiresAt = DateTime.UtcNow.AddDays(_tokens.RefreshExpiryDays) };

        try
        {
            await _userRepo.AddAsync(user);
        }
        catch (DbUpdateException)
        {
            // Concurrent registration won the unique-index race between the checks above and here.
            return ApiResponse<AuthResponse>.Fail(takenMessage);
        }
        await SendVerifyEmail(user, verifyToken);

        return ApiResponse<AuthResponse>.Ok(new AuthResponse(accessToken, refreshToken, TokenService.MapToDto(user)));
    }

    // A precomputed BCrypt hash of a random value, used to equalize timing when the user does not exist.
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("timing-equalizer-not-a-real-password");

    public async Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request, string? ip)
    {
        // Trim so trailing/leading whitespace can't turn a valid username into a "not found".
        var user = await _userRepo.GetByUsernameAsync(request.Username.Trim());
        if (user == null)
        {
            // Equalize response time so a missing username can't be distinguished from a wrong password.
            BCrypt.Net.BCrypt.Verify(request.Password, DummyHash);
            return ApiResponse<AuthResponse>.Fail("Invalid username or password.");
        }

        var passwordValid = user.PasswordHash != null && BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        // Account-state messages (locked/suspended) are only revealed to a caller who supplied the
        // correct password — i.e. the real owner. Anyone else always gets the generic failure, so
        // login can't be used to enumerate which accounts exist or are locked/banned.
        if (user.Security.LockoutEnd.HasValue && user.Security.LockoutEnd.Value > DateTime.UtcNow)
            return passwordValid
                ? ApiResponse<AuthResponse>.Fail("Account temporarily locked due to failed login attempts. Try again later.")
                : ApiResponse<AuthResponse>.Fail("Invalid username or password.");

        if (!passwordValid)
        {
            user.Security.FailedLoginAttempts++;
            if (user.Security.FailedLoginAttempts >= 5)
                user.Security.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
            await _userRepo.UpdateAsync(user);
            return ApiResponse<AuthResponse>.Fail("Invalid username or password.");
        }

        if (user.IsBanned)
            return ApiResponse<AuthResponse>.Fail("Account is suspended.");

        // Hard email-verification gate: a local (password) account with an unverified email can't
        // sign in until it verifies. Admins and Google/linked accounts (already verified) bypass.
        // The password was correct here, so this is the genuine owner — quietly resend a fresh link.
        if (user.Role != "Admin" && user.PasswordHash != null && !user.IsEmailVerified && !string.IsNullOrEmpty(user.Email))
        {
            user.Security.FailedLoginAttempts = 0;
            user.Security.LockoutEnd = null;
            var verifyToken = TokenService.NewRawToken();
            user.EmailVerification = new() { Token = TokenService.HashToken(verifyToken), ExpiresAt = DateTime.UtcNow.AddHours(24) };
            await _userRepo.UpdateAsync(user);
            await SendVerifyEmail(user, verifyToken);
            return ApiResponse<AuthResponse>.Fail(
                "Please verify your email before signing in. We've emailed a fresh verification link to your inbox.");
        }

        // Soft-deleted accounts (PROB-4): signing in within the 90-day window cancels the pending
        // deletion and restores the account. Past the window the purge job has tombstoned it.
        if (user.IsDeleted)
        {
            user.IsDeleted = false;
            user.DeletedAt = null;
            user.DeletionConfirm = new() { Token = null, ExpiresAt = null };
        }

        var (accessToken, refreshToken) = _tokens.GenerateTokens(user);
        user.Refresh = new() { Token = TokenService.HashToken(refreshToken), ExpiresAt = DateTime.UtcNow.AddDays(_tokens.RefreshExpiryDays) };
        user.Security.FailedLoginAttempts = 0;
        user.Security.LockoutEnd = null;
        user.Security.LastLoginAt = DateTime.UtcNow;
        user.Security.LastLoginIp = ip;
        await _userRepo.UpdateAsync(user);

        return ApiResponse<AuthResponse>.Ok(new AuthResponse(accessToken, refreshToken, TokenService.MapToDto(user)));
    }

    public async Task<ApiResponse<AuthResponse>> RefreshAsync(RefreshRequest request)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
            return ApiResponse<AuthResponse>.Fail("Invalid or expired refresh token.");
        var hashed = TokenService.HashToken(request.RefreshToken);
        var user = await _userRepo.GetByRefreshTokenAsync(hashed);

        if (user == null || user.Refresh.ExpiresAt <= DateTime.UtcNow || user.IsBanned)
            return ApiResponse<AuthResponse>.Fail("Invalid or expired refresh token.");

        var (accessToken, refreshToken) = _tokens.GenerateTokens(user);
        user.Refresh = new() { Token = TokenService.HashToken(refreshToken), ExpiresAt = DateTime.UtcNow.AddDays(_tokens.RefreshExpiryDays) };
        await _userRepo.UpdateAsync(user);

        return ApiResponse<AuthResponse>.Ok(new AuthResponse(accessToken, refreshToken, TokenService.MapToDto(user)));
    }

    public async Task<ApiResponse<AuthResponse>> GoogleLoginAsync(GoogleAuthRequest request, string? ip = null)
    {
        // Client-side flow: the caller posts a Google ID token (carried in Code) obtained via GIS.
        // The web login now uses the popup-free server-side code flow (GoogleCodeExchangeAsync);
        // this path stays for the game client and other direct API consumers.
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(request.Code,
                new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { _config["Authentication:Google:ClientId"] } });

            if (payload == null)
                return ApiResponse<AuthResponse>.Fail("Invalid Google token.");

            return await IssueForGooglePayloadAsync(payload, ip);
        }
        catch (InvalidJwtException)
        {
            return ApiResponse<AuthResponse>.Fail("Invalid Google token.");
        }
    }

    /// <summary>
    /// Server-side Google OAuth 2.0 authorization-code exchange. Swaps the one-time <paramref name="code"/>
    /// from the redirect callback for tokens at Google's token endpoint, validates the returned
    /// id_token, then links/creates the user. This backs the popup-free redirect login flow so
    /// browsers that block third-party sign-in popups (notably Edge) can't break login.
    /// </summary>
    public async Task<ApiResponse<AuthResponse>> GoogleCodeExchangeAsync(string code, string redirectUri, string? ip = null)
    {
        try
        {
            var payload = await ExchangeCodeForGooglePayloadAsync(code, redirectUri);
            if (payload == null)
                return ApiResponse<AuthResponse>.Fail("Could not complete Google sign-in.");
            return await IssueForGooglePayloadAsync(payload, ip);
        }
        catch (InvalidJwtException)
        {
            return ApiResponse<AuthResponse>.Fail("Invalid Google token.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Google token endpoint unreachable.");
            return ApiResponse<AuthResponse>.Fail("Could not reach Google. Please try again.");
        }
    }

    /// <summary>
    /// Exchanges a Google authorization <paramref name="code"/> for tokens at Google's token endpoint
    /// and returns the validated id_token payload (or null when config/exchange/validation fails).
    /// Shared by the popup-free login (<see cref="GoogleCodeExchangeAsync"/>) and account-link flows.
    /// Throws <see cref="InvalidJwtException"/>/<see cref="HttpRequestException"/> for the caller to map.
    /// </summary>
    private async Task<GoogleJsonWebSignature.Payload?> ExchangeCodeForGooglePayloadAsync(string code, string redirectUri)
    {
        var clientId = _config["Authentication:Google:ClientId"];
        var clientSecret = _config["Authentication:Google:ClientSecret"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            _logger.LogError("Google sign-in is not configured (missing ClientId/ClientSecret).");
            return null;
        }

        using var res = await _httpClientFactory.CreateClient().PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = clientId!,
                ["client_secret"] = clientSecret!,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code",
            }));

        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google token exchange failed ({Status}): {Body}",
                (int)res.StatusCode, await res.Content.ReadAsStringAsync());
            return null;
        }

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        if (!doc.RootElement.TryGetProperty("id_token", out var idTokenEl)
            || idTokenEl.GetString() is not { Length: > 0 } idToken)
            return null;

        return await GoogleJsonWebSignature.ValidateAsync(idToken,
            new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { clientId } });
    }

    /// <summary>
    /// Shared tail of both Google flows: resolve the account by Google id (else link an existing
    /// verified-email account, else create a fresh one) and mint our own access/refresh tokens.
    /// <paramref name="payload"/> must already be a validated Google identity.
    /// </summary>
    private async Task<ApiResponse<AuthResponse>> IssueForGooglePayloadAsync(GoogleJsonWebSignature.Payload payload, string? ip)
    {
        var user = await _userRepo.GetByGoogleIdAsync(payload.Subject);
        if (user == null)
        {
            user = await _userRepo.GetByEmailAsync(payload.Email);
            if (user != null)
            {
                if (!payload.EmailVerified)
                    return ApiResponse<AuthResponse>.Fail(
                        "This email is already registered. Sign in with your password, then link Google from settings.");
                user.GoogleId = payload.Subject;
                user.GoogleAvatarUrl = payload.Picture;
                user.AuthProvider = "linked";
                if (!user.IsEmailVerified) user.IsEmailVerified = true;
                await _userRepo.UpdateAsync(user);
            }
            else
            {
                // Derive a handle from the email's local part, sanitized to our username rules
                // (lowercase a–z, 0–9, underscore) — a raw Gmail prefix can carry dots, "+tags", or
                // uppercase that the manual-registration validator would reject. Then, if that handle
                // is already taken (e.g. someone registered "iamuser123" the normal way), append an
                // incrementing suffix until it's free. So iamuser123@gmail.com yields "iamuser123",
                // or "iamuser1231", "iamuser1232", … when the base collides with an existing user.
                var local = payload.Email.Split('@')[0].ToLowerInvariant();
                var baseUsername = new string(local.Where(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_').ToArray());
                if (baseUsername.Length < 3) baseUsername = "user";              // empty / all-symbols → generic base
                if (baseUsername.Length > 20) baseUsername = baseUsername[..20];  // leave room for a numeric suffix
                var username = baseUsername;
                int counter = 1;
                while (!await _userRepo.IsUsernameAvailableAsync(username))
                    username = $"{baseUsername}{counter++}";

                user = new User
                {
                    Username = username,
                    Email = payload.Email,
                    IsEmailVerified = payload.EmailVerified,
                    DisplayName = payload.Name,
                    GoogleId = payload.Subject,
                    GoogleAvatarUrl = payload.Picture,
                    AuthProvider = "google",
                    PasswordHash = null
                };
                if (!await _userRepo.TryAddAsync(user))
                {
                    // Race: a concurrent first-time Google login already created this account.
                    // Fall back to the existing row instead of surfacing a 500.
                    user = await _userRepo.GetByGoogleIdAsync(payload.Subject)
                        ?? await _userRepo.GetByEmailAsync(payload.Email);
                    if (user == null)
                        return ApiResponse<AuthResponse>.Fail("Could not complete Google sign-in. Please try again.");
                }
            }
        }

        if (user.IsBanned)
            return ApiResponse<AuthResponse>.Fail("Account is suspended.");

        user.Security.LastLoginAt = DateTime.UtcNow;
        user.Security.LastLoginIp = ip;
        var (accessToken, refreshToken) = _tokens.GenerateTokens(user);
        user.Refresh = new() { Token = TokenService.HashToken(refreshToken), ExpiresAt = DateTime.UtcNow.AddDays(_tokens.RefreshExpiryDays) };
        await _userRepo.UpdateAsync(user);

        return ApiResponse<AuthResponse>.Ok(new AuthResponse(accessToken, refreshToken, TokenService.MapToDto(user)));
    }

    public async Task<ApiResponse<UserDto>> GetCurrentUserAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        return user == null
            ? ApiResponse<UserDto>.Fail("User not found.")
            : ApiResponse<UserDto>.Ok(TokenService.MapToDto(user));
    }

    public async Task<ApiResponse<SessionStatusDto>> CheckSessionAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse<SessionStatusDto>.Fail("User not found.");
        return ApiResponse<SessionStatusDto>.Ok(
            new SessionStatusDto(user.Id, user.Username, user.Role, user.IsBanned, user.Security.TokensValidAfter));
    }

    public async Task<ApiResponse<AuthResponse>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse<AuthResponse>.Fail("User not found.");

        if (user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return ApiResponse<AuthResponse>.Fail("Incorrect current password.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.MustChangePassword = false;

        // Invalidate every session issued before now: other devices' access tokens fail the next
        // session-check ("sat" < TokensValidAfter → 401) and their refresh token is already gone.
        user.Security.TokensValidAfter = DateTime.UtcNow;

        // Mint a fresh session for THIS device so the user who just changed their password stays
        // signed in (its "sat" is >= TokensValidAfter). Everyone else is booted.
        var (accessToken, refreshToken) = _tokens.GenerateTokens(user);
        user.Refresh = new() { Token = TokenService.HashToken(refreshToken), ExpiresAt = DateTime.UtcNow.AddDays(_tokens.RefreshExpiryDays) };
        await _userRepo.UpdateAsync(user);

        // Security confirmation email — best-effort, never blocks the change. Lets the owner react if
        // the change was not made by them.
        if (!string.IsNullOrEmpty(user.Email))
            await TrySend(user.Email, "Your Attrition password was changed",
                $"Hi {user.Username},\n\nThe password on your Attrition account was just changed. " +
                "If you made this change, no action is needed.\n\nIf this was NOT you, reset your password " +
                "immediately using \"Forgot password\" and secure your email account.");

        return ApiResponse<AuthResponse>.Ok(new AuthResponse(accessToken, refreshToken, TokenService.MapToDto(user)));
    }

    public async Task<ApiResponse> LogoutAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse.Fail("User not found.");

        user.Refresh = new() { Token = null, ExpiresAt = null };
        await _userRepo.UpdateAsync(user);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        if (string.IsNullOrEmpty(request.Email)) return ApiResponse.Fail("Email is required.");

        var generic = ApiResponse.Ok();

        var user = await _userRepo.GetByEmailAsync(request.Email);
        if (user == null) return generic;

        var resetToken = TokenService.NewRawToken();
        user.PasswordReset = new() { Token = TokenService.HashToken(resetToken), ExpiresAt = DateTime.UtcNow.AddHours(1) };
        await _userRepo.UpdateAsync(user);

        var clientUrl = _config["App:ClientUrl"] ?? "http://localhost:3000";
        var resetUrl = $"{clientUrl}/reset-password?token={Uri.EscapeDataString(resetToken)}";
        await TrySend(user.Email!, "Reset Your Attrition Password",
            $"Hi {user.Username},\n\nYou requested a password reset. Reset it here: {resetUrl}");
        return generic;
    }

    public async Task<ApiResponse> ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (string.IsNullOrEmpty(request.Token)) return ApiResponse.Fail("Reset token is required.");

        var hashed = TokenService.HashToken(request.Token);
        var user = await _userRepo.GetByPasswordResetTokenAsync(hashed);
        if (user == null || user.PasswordReset.ExpiresAt <= DateTime.UtcNow)
            return ApiResponse.Fail("Invalid or expired password reset token.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordReset = new() { Token = null, ExpiresAt = null };
        user.MustChangePassword = false;
        user.Refresh = new() { Token = null, ExpiresAt = null };
        // A password reset invalidates every existing session — the account may have been
        // compromised, so all devices must sign in again with the new password.
        user.Security.TokensValidAfter = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> VerifyEmailAsync(VerifyEmailRequest request)
    {
        if (string.IsNullOrEmpty(request.Token)) return ApiResponse.Fail("Verification token is required.");

        var user = await _userRepo.GetByEmailVerificationTokenAsync(TokenService.HashToken(request.Token));
        if (user == null) return ApiResponse.Fail("Invalid verification token.");
        if (user.EmailVerification.ExpiresAt.HasValue && user.EmailVerification.ExpiresAt.Value <= DateTime.UtcNow)
            return ApiResponse.Fail("Verification token has expired. Please request a new one.");

        if (!string.IsNullOrEmpty(user.PendingEmail))
        {
            user.Email = user.PendingEmail;
            user.PendingEmail = null;
        }
        user.IsEmailVerified = true;
        user.EmailVerification = new() { Token = null, ExpiresAt = null };
        await _userRepo.UpdateAsync(user);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> SendVerificationEmailAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse.Fail("User not found.");
        if (string.IsNullOrEmpty(user.Email)) return ApiResponse.Fail("No email address registered for this account.");
        if (user.IsEmailVerified) return ApiResponse.Fail("Email is already verified.");

        var verifyToken = TokenService.NewRawToken();
        user.EmailVerification = new() { Token = TokenService.HashToken(verifyToken), ExpiresAt = DateTime.UtcNow.AddHours(24) };
        await _userRepo.UpdateAsync(user);

        await SendVerifyEmail(user, verifyToken);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> LinkGoogleAsync(Guid userId, GoogleAuthRequest request)
    {
        // ID-token variant (game client / direct API consumers).
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(request.Code,
                new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { _config["Authentication:Google:ClientId"] } });
            if (payload == null) return ApiResponse.Fail("Invalid Google token.");
            return await LinkPayloadToUserAsync(userId, payload);
        }
        catch (InvalidJwtException)
        {
            return ApiResponse.Fail("Invalid Google token.");
        }
    }

    /// <summary>
    /// Server-side (redirect flow) account link: exchange the authorization <paramref name="code"/>
    /// for a validated Google identity, then attach it to <paramref name="userId"/>. Mirrors the
    /// popup-free login flow so account-linking behaves consistently across browsers.
    /// </summary>
    public async Task<ApiResponse> LinkGoogleByCodeAsync(Guid userId, string code, string redirectUri)
    {
        try
        {
            var payload = await ExchangeCodeForGooglePayloadAsync(code, redirectUri);
            if (payload == null) return ApiResponse.Fail("Could not connect your Google account. Please try again.");
            return await LinkPayloadToUserAsync(userId, payload);
        }
        catch (InvalidJwtException)
        {
            return ApiResponse.Fail("Invalid Google token.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Google token endpoint unreachable.");
            return ApiResponse.Fail("Could not reach Google. Please try again.");
        }
    }

    /// <summary>Attaches a validated Google identity to an existing account (shared by both link flows).</summary>
    private async Task<ApiResponse> LinkPayloadToUserAsync(Guid userId, GoogleJsonWebSignature.Payload payload)
    {
        var existing = await _userRepo.GetByGoogleIdAsync(payload.Subject);
        if (existing != null && existing.Id != userId)
            return ApiResponse.Fail("That Google account is already linked to another user.");

        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse.Fail("User not found.");

        user.GoogleId = payload.Subject;
        user.GoogleAvatarUrl = payload.Picture;
        user.AuthProvider = "linked";
        if (string.IsNullOrEmpty(user.Email))
        {
            user.Email = payload.Email;
            user.IsEmailVerified = payload.EmailVerified;
        }
        await _userRepo.UpdateAsync(user);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> UnlinkGoogleAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse.Fail("User not found.");

        if (string.IsNullOrEmpty(user.PasswordHash))
            return ApiResponse.Fail("Cannot unlink Google account without setting a password first.");

        user.GoogleId = null;
        user.GoogleAvatarUrl = null;
        user.AuthProvider = "local";
        await _userRepo.UpdateAsync(user);
        return ApiResponse.Ok();
    }

    private Task SendVerifyEmail(User user, string verifyToken)
    {
        if (string.IsNullOrEmpty(user.Email)) return Task.CompletedTask;
        var clientUrl = _config["App:ClientUrl"] ?? "http://localhost:3000";
        var verifyUrl = $"{clientUrl}/verify-email?token={Uri.EscapeDataString(verifyToken)}";
        return TrySend(user.Email, "Verify Your Attrition Account",
            $"Hi {user.Username},\n\nPlease verify your email: {verifyUrl}");
    }

    private async Task TrySend(string to, string subject, string body)
    {
        try { await _email.SendAsync(to, subject, body); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to send email to {To}", to); }
    }
}
