using Microsoft.AspNetCore.Http;

namespace Identity.Service.Services;

public class FileService : IFileService
{
    private static readonly string[] ImageExts = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private readonly IFileStorage _storage;
    private readonly long _maxAvatarSize;
    private const long MaxBackgroundSize = 10 * 1024 * 1024;

    public FileService(IConfiguration config, IFileStorage storage)
    {
        _storage = storage;
        _maxAvatarSize = long.Parse(config["FileUpload:MaxAvatarSizeMB"] ?? "5") * 1024 * 1024;
    }

    public Task<(bool, string?, string?)> UploadAvatarAsync(Guid userId, IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        return SaveImageAsync(file, "avatars", $"{userId}{ext}", _maxAvatarSize);
    }

    public Task<(bool, string?, string?)> UploadBackgroundAsync(Guid userId, IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        return SaveImageAsync(file, "backgrounds", $"{userId}{ext}", MaxBackgroundSize);
    }

    private async Task<(bool, string?, string?)> SaveImageAsync(IFormFile file, string subfolder, string fileName, long maxBytes)
    {
        if (file == null || file.Length == 0) return (false, null, "File is empty.");
        if (file.Length > maxBytes) return (false, null, $"File exceeds maximum allowed size of {maxBytes / (1024 * 1024)}MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!ImageExts.Contains(ext)) return (false, null, "Invalid file type.");

        await using var stream = file.OpenReadStream();
        var url = await _storage.SaveAsync(subfolder, fileName, stream);
        return (true, url, null);
    }
}
