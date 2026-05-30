using Microsoft.AspNetCore.Http;

namespace Identity.Service.Services;

public interface IFileStorage
{
    Task<string> SaveAsync(string subfolder, string fileName, Stream stream);
    Task<bool> DeleteAsync(string relativePath);
}

public interface IFileService
{
    Task<(bool Success, string? Path, string? Error)> UploadAvatarAsync(Guid userId, IFormFile file);
    Task<(bool Success, string? Path, string? Error)> UploadBackgroundAsync(Guid userId, IFormFile file);
    Task DeleteAsync(string? path);
}

public interface IEmailService
{
    Task SendAsync(string to, string subject, string body);
}
