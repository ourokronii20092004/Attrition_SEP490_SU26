using Microsoft.AspNetCore.Http;

namespace Identity.Service.Services;

public class FileService : IFileService
{
    private static readonly string[] ImageExts = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    // Magic-byte signatures so a renamed non-image (e.g. an .exe called avatar.png) is rejected
    // rather than stored and served. Extension alone is client-controlled and untrustworthy.
    private static readonly Dictionary<string, byte[][]> ImageSignatures = new()
    {
        [".jpg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".png"] = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
        [".gif"] = new[] { new byte[] { 0x47, 0x49, 0x46 } },
        [".webp"] = new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } },
    };

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
        if (file is null || file.Length == 0) return Task.FromResult((false, (string?)null, (string?)"File is empty."));
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        // Unique filename per upload so a replacement gets a fresh URL — a deterministic
        // "{userId}{ext}" name made the browser serve the cached old image on replace.
        return SaveImageAsync(file, "avatars", $"{userId}-{Guid.NewGuid():N}{ext}", _maxAvatarSize);
    }

    public Task<(bool, string?, string?)> UploadBackgroundAsync(Guid userId, IFormFile file)
    {
        if (file is null || file.Length == 0) return Task.FromResult((false, (string?)null, (string?)"File is empty."));
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        return SaveImageAsync(file, "backgrounds", $"{userId}-{Guid.NewGuid():N}{ext}", MaxBackgroundSize);
    }

    private async Task<(bool, string?, string?)> SaveImageAsync(IFormFile file, string subfolder, string fileName, long maxBytes)
    {
        if (file == null || file.Length == 0) return (false, null, "File is empty.");
        if (file.Length > maxBytes) return (false, null, $"File exceeds maximum allowed size of {maxBytes / (1024 * 1024)}MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!ImageExts.Contains(ext)) return (false, null, "Invalid file type.");

        await using var stream = file.OpenReadStream();
        if (!await LooksLikeImageAsync(stream, ext))
            return (false, null, "File content does not match its image extension.");

        var url = await _storage.SaveAsync(subfolder, fileName, stream);
        return (true, url, null);
    }

    private static async Task<bool> LooksLikeImageAsync(Stream stream, string ext)
    {
        if (!ImageSignatures.TryGetValue(ext, out var signatures)) return false;

        var header = new byte[8];
        var read = await stream.ReadAsync(header);
        if (stream.CanSeek) stream.Position = 0; // rewind for the subsequent save
        return signatures.Any(sig => read >= sig.Length && header.AsSpan(0, sig.Length).SequenceEqual(sig));
    }

    // Best-effort delete of a previously stored file (the public URL stored on the user).
    public async Task DeleteAsync(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try { await _storage.DeleteAsync(path); } catch { /* best-effort cleanup */ }
    }
}
