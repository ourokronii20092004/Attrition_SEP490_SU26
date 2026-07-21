namespace Identity.Service.DTOs;

public record UpdateProfileRequest(string? Bio, string? Email, bool? NotifyOnReply, bool? NotifyOnMention, string? DisplayName);
public record UpdateThemeRequest(string ThemeMode, string ThemeAccent);
public record SetPasswordRequest(string NewPassword);
public record UpdateEmailRequest(string NewEmail, string CurrentPassword);
// Account deletion (PROB-4): a confirmed, 90-day-recoverable flow rather than an instant wipe.
public record ConfirmDeletionRequest(string Token);
