namespace Attrition.API.DTOs;

public record RegisterRequest(string Username, string Password, string? Email);
public record GoogleAuthRequest(string Code, string RedirectUri);
public record LinkGoogleRequest(string Code, string RedirectUri);
public record LoginRequest(string Username, string Password);
public record AuthResponse(string AccessToken, string RefreshToken, UserDto User);
public record RefreshRequest(string RefreshToken);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);