namespace Identity.Service.DTOs;

public record UpdateProfileRequest(string? Bio, string? Email, bool? NotifyOnReply, bool? NotifyOnMention,
    string? DisplayName, bool? ShowBio = null, bool? ShowActivity = null);
public record UpdateThemeRequest(string ThemeMode, string ThemeAccent);
public record SetPasswordRequest(string NewPassword);
public record UpdateEmailRequest(string NewEmail, string CurrentPassword);
// Account deletion (PROB-4): a confirmed, 90-day-recoverable flow rather than an instant wipe.
public record ConfirmDeletionRequest(string Token);

/// <summary>
/// Error text the profile lookup uses to mean "exists, but the owner hid it". The controller
/// compares against this constant to answer 403 instead of 404, so the client can show a
/// "hidden profile" page rather than a generic not-found.
/// </summary>
public static class ProfileErrors
{
    public const string Hidden = "This user has hidden their profile.";
}