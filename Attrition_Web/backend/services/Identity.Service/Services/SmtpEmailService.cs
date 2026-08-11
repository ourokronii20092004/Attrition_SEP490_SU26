using System.Net;
using System.Net.Mail;

namespace Identity.Service.Services;

/// <summary>
/// SMTP email transport (Gmail-compatible). Configured via the Smtp:* settings;
/// when host/credentials are absent the caller falls back to ConsoleEmailService.
/// </summary>
public sealed class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public static bool IsConfigured(IConfiguration config) =>
        !string.IsNullOrWhiteSpace(config["Smtp:Host"])
        && !string.IsNullOrWhiteSpace(config["Smtp:Username"])
        && !string.IsNullOrWhiteSpace(config["Smtp:Password"]);

    public async Task SendAsync(string to, string subject, string body, string? htmlBody = null)
    {
        var host = _config["Smtp:Host"]!;
        var port = int.TryParse(_config["Smtp:Port"], out var p) ? p : 587;
        var username = _config["Smtp:Username"]!;
        var password = _config["Smtp:Password"]!;
        var fromAddress = _config["Smtp:FromAddress"] ?? username;
        var fromName = _config["Smtp:FromName"] ?? "Attrition";
        var enableSsl = !bool.TryParse(_config["Smtp:EnableSsl"], out var ssl) || ssl;

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = subject,
            // The plain-text version stays the message body, so a client that ignores alternate
            // views still shows something readable rather than raw markup.
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(to);

        // Offer HTML as an alternate view: clients that render it pick the styled version, the
        // rest fall back to the text body above.
        if (!string.IsNullOrWhiteSpace(htmlBody))
        {
            var html = AlternateView.CreateAlternateViewFromString(
                htmlBody, System.Text.Encoding.UTF8, "text/html");
            message.AlternateViews.Add(html);
        }

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            Credentials = new NetworkCredential(username, password)
        };

        try
        {
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            // Never surface SMTP failures to the caller — email is best-effort for account flows.
            _logger.LogWarning(ex, "Failed to send email to {To}", to);
        }
    }
}