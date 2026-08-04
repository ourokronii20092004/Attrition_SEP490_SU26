namespace Identity.Service.Services.Interface;

public interface IEmailService
{
    /// <summary>
    /// Sends an account email. <paramref name="htmlBody"/> is optional: when supplied the message
    /// goes out as multipart (HTML + the plain-text <paramref name="body"/>), so clients with HTML
    /// disabled — and anything that indexes mail as text — still get a readable message.
    /// </summary>
    Task SendAsync(string to, string subject, string body, string? htmlBody = null);
}
