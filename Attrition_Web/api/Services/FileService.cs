using Attrition.API.DTOs;

namespace Attrition.API.Services;

public class FileService
{
    private readonly IConfiguration _config;
    private readonly string _uploadPath;
    private readonly long _maxSize;

    public FileService(IConfiguration config)
    {
        _config = config;
        _uploadPath = _config["FileUpload:UploadPath"] ?? "./uploads";
        _maxSize = long.Parse(_config["FileUpload:MaxAvatarSizeMB"] ?? "5") * 1024 * 1024;
    }

    public async Task<ApiResponse<string>> UploadAvatarAsync(Guid userId, IFormFile file)
    {
        if (file.Length == 0) return new ApiResponse<string>(false, Error: "File is empty.");
        if (file.Length > _maxSize) return new ApiResponse<string>(false, Error: "File exceeds max size.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        if (!allowedExts.Contains(ext)) return new ApiResponse<string>(false, Error: "Invalid file type.");

        var uploadDir = Path.Combine(_uploadPath, "avatars");
        if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

        var fileName = $"{userId}{ext}";
        var filePath = Path.Combine(uploadDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var avatarUrl = $"/uploads/avatars/{fileName}";
        return new ApiResponse<string>(true, avatarUrl);
    }

    public async Task<ApiResponse<string>> UploadBackgroundAsync(Guid userId, IFormFile file)
    {
        if (file.Length == 0) return new ApiResponse<string>(false, Error: "File is empty.");
        // Allow slightly larger size for backgrounds (e.g. 10MB)
        if (file.Length > 10 * 1024 * 1024) return new ApiResponse<string>(false, Error: "File exceeds max size.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        if (!allowedExts.Contains(ext)) return new ApiResponse<string>(false, Error: "Invalid file type.");

        var uploadDir = Path.Combine(_uploadPath, "backgrounds");
        if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

        var fileName = $"{userId}{ext}";
        var filePath = Path.Combine(uploadDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var bgUrl = $"/uploads/backgrounds/{fileName}";
        return new ApiResponse<string>(true, bgUrl);
    }

    public async Task<ApiResponse<string>> UploadContentImageAsync(IFormFile file)
    {
        if (file.Length == 0) return new ApiResponse<string>(false, Error: "File is empty.");
        if (file.Length > 10 * 1024 * 1024) return new ApiResponse<string>(false, Error: "File exceeds 10MB limit.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        if (!allowedExts.Contains(ext)) return new ApiResponse<string>(false, Error: "Invalid file type.");

        var uploadDir = Path.Combine(_uploadPath, "content");
        if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var imageUrl = $"/uploads/content/{fileName}";
        return new ApiResponse<string>(true, imageUrl);
    }
}