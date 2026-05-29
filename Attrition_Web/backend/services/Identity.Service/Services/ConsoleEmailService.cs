namespace Identity.Service.Services;

/// <summary>Stub email transport — writes messages to stdout. Swap for SMTP/provider later.</summary>
public class ConsoleEmailService : IEmailService
{
    private readonly ILogger<ConsoleEmailService> _logger;

    public ConsoleEmailService(ILogger<ConsoleEmailService> logger) => _logger = logger;

    public Task SendAsync(string to, string subject, string body)
    {
        _logger.LogInformation("EMAIL → {To}\nSubject: {Subject}\n{Body}", to, subject, body);
        return Task.CompletedTask;
    }
}
