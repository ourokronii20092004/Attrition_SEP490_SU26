namespace Identity.Service.Services.Interface;

public interface IFileService
{
    Task<(bool Success, string? Path, string? Error)> UploadAvatarAsync(Guid userId, IFormFile file);

    Task<(bool Success, string? Path, string? Error)> UploadBackgroundAsync(Guid userId, IFormFile file);

    Task DeleteAsync(string? path);
}