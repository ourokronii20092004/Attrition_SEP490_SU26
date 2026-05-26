using Attrition.API.DTOs;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Attrition.API.Services;

public class FileService : IFileService
{
    private readonly IFileStorage _storage;
    private readonly long _maxAvatarSize;

    public FileService(IConfiguration config, IFileStorage storage)
    {
        _storage = storage;
        _maxAvatarSize = long.Parse(config["FileUpload:MaxAvatarSizeMB"] ?? "5") * 1024 * 1024;
    }

    public async Task<ApiResponse<string>> UploadAvatarAsync(Guid userId, IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var fileName = $"{userId}{ext}";
        return await UploadGenericFileInternalAsync(file, "avatars", fileName, allowedExts, _maxAvatarSize);
    }

    public async Task<ApiResponse<string>> UploadBackgroundAsync(Guid userId, IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var fileName = $"{userId}{ext}";
        return await UploadGenericFileInternalAsync(file, "backgrounds", fileName, allowedExts, 10 * 1024 * 1024);
    }

    public async Task<ApiResponse<string>> UploadContentImageAsync(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var fileName = $"{Guid.NewGuid()}{ext}";
        return await UploadGenericFileInternalAsync(file, "content", fileName, allowedExts, 10 * 1024 * 1024);
    }

    public async Task<ApiResponse<string>> UploadDocumentAsync(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExts = new[] { ".pdf", ".doc", ".docx", ".txt", ".md" };
        var fileName = $"{Guid.NewGuid()}{ext}";
        return await UploadGenericFileInternalAsync(file, "documents", fileName, allowedExts, 20 * 1024 * 1024);
    }

    public async Task<ApiResponse<string>> UploadGenericFileAsync(IFormFile file, string subfolder, string[] allowedExtensions, long maxSizeBytes)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid()}{ext}";
        return await UploadGenericFileInternalAsync(file, subfolder, fileName, allowedExtensions, maxSizeBytes);
    }

    private async Task<ApiResponse<string>> UploadGenericFileInternalAsync(IFormFile file, string subfolder, string fileName, string[] allowedExtensions, long maxSizeBytes)
    {
        if (file == null || file.Length == 0) return new ApiResponse<string>(false, Error: "File is empty.");
        if (file.Length > maxSizeBytes) return new ApiResponse<string>(false, Error: $"File exceeds maximum allowed size of {maxSizeBytes / (1024 * 1024)}MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext)) return new ApiResponse<string>(false, Error: "Invalid file type.");

        using (var stream = file.OpenReadStream())
        {
            var url = await _storage.SaveAsync(subfolder, fileName, stream);
            return new ApiResponse<string>(true, url);
        }
    }
}