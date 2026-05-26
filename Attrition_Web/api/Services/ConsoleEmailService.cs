using System;
using System.Threading.Tasks;

namespace Attrition.API.Services;

public class ConsoleEmailService : IEmailService
{
    public Task SendAsync(string to, string subject, string body)
    {
        Console.WriteLine("==========================================");
        Console.WriteLine("EMAIL STUB SENDING:");
        Console.WriteLine($"To:      {to}");
        Console.WriteLine($"Subject: {subject}");
        Console.WriteLine($"Body:    {body}");
        Console.WriteLine("==========================================");
        return Task.CompletedTask;
    }
}
