using Attrition.API.DTOs;

namespace Attrition.API.Services;

public interface IFileService
{
    Task<ApiResponse<string>> UploadAvatarAsync(Guid userId, IFormFile file);
    Task<ApiResponse<string>> UploadBackgroundAsync(Guid userId, IFormFile file);
    Task<ApiResponse<string>> UploadContentImageAsync(IFormFile file);
    Task<ApiResponse<string>> UploadDocumentAsync(IFormFile file);
    Task<ApiResponse<string>> UploadGenericFileAsync(IFormFile file, string subfolder, string[] allowedExtensions, long maxSizeBytes);
}

