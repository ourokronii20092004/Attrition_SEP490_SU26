namespace Identity.Service.Services;

/// <summary>Stub email transport — writes messages to stdout. Swap for SMTP/provider later.</summary>
public class ConsoleEmailService : IEmailService
{
    private readonly ILogger<ConsoleEmailService> _logger;

    public ConsoleEmailService(ILogger<ConsoleEmailService> logger) => _logger = logger;

    public Task SendAsync(string to, string subject, string body)
    {
        // Body may contain reset/verify URLs with raw tokens — never log it at Information.
        _logger.LogInformation("EMAIL → {To}\nSubject: {Subject}", to, subject);
        return Task.CompletedTask;
    }
}
