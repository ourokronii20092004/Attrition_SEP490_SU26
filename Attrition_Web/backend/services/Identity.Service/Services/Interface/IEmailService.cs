namespace Identity.Service.Services.Interface;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string body);
}
