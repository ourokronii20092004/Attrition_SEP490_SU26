namespace Identity.Service.DTOs;

public record RegisterRequest(string Username, string Password, string? Email);
public record GoogleAuthRequest(string Code, string RedirectUri);
public record LoginRequest(string Username, string Password, bool RememberMe = true);
public record AuthResponse(string AccessToken, string RefreshToken, UserDto User);
public record RefreshRequest(string? RefreshToken = null);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string NewPassword);
public record VerifyEmailRequest(string Token);
