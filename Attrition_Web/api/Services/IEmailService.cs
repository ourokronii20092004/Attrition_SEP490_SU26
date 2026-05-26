using System.Threading.Tasks;

namespace Attrition.API.Services;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string body);
}
