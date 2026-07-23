using System.Linq.Expressions;
using System.Security.Cryptography;
using Assets.Service.DTOs;
using Assets.Service.Models;
using Assets.Service.Repositories;
using BuildingBlocks.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Assets.Service.Services;

public class AssetService : IAssetService
{
    private static readonly string[] ImageExts = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private static readonly string[] DocExts = { ".pdf", ".doc", ".docx", ".txt", ".md" };

    private readonly IAssetRepository _repo;
    private readonly IFileStorage _storage;
    private readonly ILogger<AssetService> _logger;
    private readonly long _maxSize;

    public AssetService(IAssetRepository repo, IFileStorage storage, IConfiguration config, ILogger<AssetService> logger)
    {
        _repo = repo;
        _storage = storage;
        _logger = logger;
        var mb = long.TryParse(config["FileUpload:MaxImageSizeMB"], out var v) && v > 0 ? v : 20;
        _maxSize = mb * 1024 * 1024;
    }

    public async Task<AssetDto?> GetAssetAsync(Guid assetId)
    {
        var a = await _repo.GetByIdAsync(assetId);
        return a == null ? null : ToDto(a);
    }

    public async Task<PaginatedResponse<AssetDto>> ListAssetsAsync(int page, int pageSize, string? assetType, string? search)
    {
        var search_ = search?.ToLower();
        Expression<Func<Asset, bool>>? filter = (assetType, search_) switch
        {
            (string t, string s) => a => a.AssetType == t && a.FileName.ToLower().Contains(s),
            (string t, null) => a => a.AssetType == t,
            (null, string s) => a => a.FileName.ToLower().Contains(s),
            _ => null
        };

        var (items, total) = await _repo.GetPagedAsync(page, pageSize, filter,
            q => q.OrderByDescending(a => a.UploadedAt));

        return new PaginatedResponse<AssetDto>(items.Select(ToDto).ToList(), total, page, pageSize);
    }

    public async Task<ApiResponse<AssetDto>> UploadAssetAsync(IFormFile file, string assetType,
        string? title, string? description, string? tags, Guid userId, string userName,
        string? sourceType = null, string? sourceId = null)
    {
        if (file == null || file.Length == 0)
            return ApiResponse<AssetDto>.Fail("File is empty.");
        if (file.Length > _maxSize)
            return ApiResponse<AssetDto>.Fail($"File exceeds the maximum allowed size of {_maxSize / (1024 * 1024)}MB.");

        if (string.IsNullOrWhiteSpace(assetType) || assetType.Length > 50)
            return ApiResponse<AssetDto>.Fail("Asset type is required and must be at most 50 characters.");
        if (title is { Length: > 200 })
            return ApiResponse<AssetDto>.Fail("Title must be at most 200 characters.");
        if (description is { Length: > 2000 })
            return ApiResponse<AssetDto>.Fail("Description must be at most 2000 characters.");
        if (tags is { Length: > 500 })
            return ApiResponse<AssetDto>.Fail("Tags must be at most 500 characters.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        // Document/lore types may be docs OR images; everything else is treated as an image.
        string subfolder;
        if (assetType is "document" or "lore")
        {
            if (!ImageExts.Contains(ext) && !DocExts.Contains(ext))
                return ApiResponse<AssetDto>.Fail("Invalid file type for a document/lore asset.");
            subfolder = "documents";
        }
        else
        {
            if (!ImageExts.Contains(ext))
                return ApiResponse<AssetDto>.Fail("Invalid image file type.");
            subfolder = "assets";
        }

        var fileName = $"{Guid.NewGuid()}{ext}";
        string storedPath;
        await using (var stream = file.OpenReadStream())
        {
            var (matches, detected) = await ContentMatchesExtensionAsync(stream, ext);
            if (!matches)
                return ApiResponse<AssetDto>.Fail(
                    $"File content does not match its extension. Expected a {ExpectedLabel(ext)} (from the '{ext}' extension), but the file's contents look like {detected}.");
            stream.Position = 0;
            storedPath = await _storage.SaveAsync(subfolder, fileName, stream);
        }

        var asset = new Asset
        {
            FileName = file.FileName,
            FilePath = storedPath,
            AssetType = assetType,
            MimeType = ResolveMime(ext),
            FileSize = file.Length,
            Title = title,
            Description = description,
            Tags = tags,
            SourceType = sourceType,
            SourceId = sourceId,
            UploadedById = userId,
            UploadedByName = userName
        };
        try
        {
            await _repo.AddAsync(asset);
        }
        catch (Exception ex)
        {
            // The file is already on disk; if the row never persisted, delete it so we don't leak
            // an orphaned blob with no DB record pointing at it.
            _logger.LogError(ex, "Asset DB insert failed; cleaning up stored file {Path}", storedPath);
            try { await _storage.DeleteAsync(storedPath); }
            catch (Exception cleanupEx) { _logger.LogWarning(cleanupEx, "Failed to clean up orphaned asset file {Path}", storedPath); }
            throw;
        }

        return ApiResponse<AssetDto>.Ok(ToDto(asset));
    }

    public async Task<ApiResponse<AssetDto>> UploadUnitySourceAsync(IFormFile file, string sourceType,
        string sourceId, Guid userId, string userName)
    {
        if (sourceType is not ("item" or "skill" or "enemy"))
            return ApiResponse<AssetDto>.Fail("Source type must be item, skill, or enemy.");
        if (string.IsNullOrWhiteSpace(sourceId) || sourceId.Length > 64 ||
            !System.Text.RegularExpressions.Regex.IsMatch(sourceId, "^[a-z0-9]+(?:_[a-z0-9]+)*$"))
            return ApiResponse<AssetDto>.Fail("Source ID must be canonical lower_snake_case up to 64 characters.");
        if (file == null || file.Length == 0)
            return ApiResponse<AssetDto>.Fail("File is empty.");
        if (file.Length > _maxSize)
            return ApiResponse<AssetDto>.Fail($"File exceeds the maximum allowed size of {_maxSize / (1024 * 1024)}MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!ImageExts.Contains(ext)) return ApiResponse<AssetDto>.Fail("Invalid image file type.");

        string hash;
        await using (var hashStream = file.OpenReadStream())
        {
            var (matches, detected) = await ContentMatchesExtensionAsync(hashStream, ext);
            if (!matches)
                return ApiResponse<AssetDto>.Fail($"File content does not match its extension; detected {detected}.");
            hashStream.Position = 0;
            hash = Convert.ToHexString(await SHA256.HashDataAsync(hashStream)).ToLowerInvariant();
        }

        var existing = await _repo.GetBySourceAsync($"unity-{sourceType}", sourceId);
        if (existing?.ContentHash == hash) return ApiResponse<AssetDto>.Ok(ToDto(existing));
        var creating = existing == null;

        var fileName = $"{Guid.NewGuid()}{ext}";
        string newPath;
        await using (var stream = file.OpenReadStream())
            newPath = await _storage.SaveAsync("assets", fileName, stream);

        try
        {
            if (existing == null)
            {
                existing = new Asset
                {
                    FileName = file.FileName, FilePath = newPath, AssetType = "sprite",
                    MimeType = ResolveMime(ext), FileSize = file.Length, Title = sourceId,
                    Tags = $"unity,{sourceType}", SourceType = $"unity-{sourceType}", SourceId = sourceId,
                    ContentHash = hash, UploadedById = userId, UploadedByName = userName
                };
                await _repo.AddTrackedAsync(existing);
                await _repo.SaveAsync();
            }
            else
            {
                existing.FileName = file.FileName;
                existing.FilePath = newPath;
                existing.MimeType = ResolveMime(ext);
                existing.FileSize = file.Length;
                existing.ContentHash = hash;
                existing.UpdatedAt = DateTime.UtcNow;
                await _repo.SaveAsync();
            }
        }
        catch (DbUpdateException) when (creating && existing != null)
        {
            await _storage.DeleteAsync(newPath);
            _repo.Detach(existing);
            var winner = await _repo.GetBySourceAsync($"unity-{sourceType}", sourceId);
            return winner == null
                ? ApiResponse<AssetDto>.Fail("Concurrent upload conflict. Retry the upload.")
                : ApiResponse<AssetDto>.Ok(ToDto(winner));
        }
        catch
        {
            await _storage.DeleteAsync(newPath);
            throw;
        }

        // ponytail: Retain replaced source files so a failed metadata import cannot break the live URL; add reference-aware cleanup when storage growth warrants it.
        return ApiResponse<AssetDto>.Ok(ToDto(existing));
    }

    public async Task<ApiResponse<string>> UploadInlineImageAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return ApiResponse<string>.Fail("File is empty.");
        if (file.Length > _maxSize)
            return ApiResponse<string>.Fail($"Image exceeds the maximum allowed size of {_maxSize / (1024 * 1024)}MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!ImageExts.Contains(ext))
            return ApiResponse<string>.Fail("Only image files are allowed.");

        var fileName = $"{Guid.NewGuid()}{ext}";
        await using var stream = file.OpenReadStream();
        var (matches, detected) = await ContentMatchesExtensionAsync(stream, ext);
        if (!matches)
            return ApiResponse<string>.Fail(
                $"File content does not match its extension. Expected a {ExpectedLabel(ext)} (from the '{ext}' extension), but the file's contents look like {detected}.");
        stream.Position = 0;
        // Stored under inline/ (not the gallery) — no Asset row, just the public URL.
        var url = await _storage.SaveAsync("inline", fileName, stream);
        return ApiResponse<string>.Ok(url);
    }

    public async Task<ApiResponse> UpdateAssetAsync(Guid assetId, UpdateAssetReq req)
    {
        var asset = await _repo.GetByIdAsync(assetId);
        if (asset == null) return ApiResponse.Fail("Asset not found.");

        if (req.Title != null) asset.Title = req.Title;
        if (req.Description != null) asset.Description = req.Description;
        if (req.Tags != null) asset.Tags = req.Tags;
        if (req.AssetType != null) asset.AssetType = req.AssetType;
        asset.UpdatedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(asset);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> DeleteAssetAsync(Guid assetId)
    {
        var asset = await _repo.GetByIdAsync(assetId);
        if (asset == null) return ApiResponse.Fail("Asset not found.");

        await _storage.DeleteAsync(asset.FilePath);
        await _repo.DeleteAsync(asset);
        return ApiResponse.Ok();
    }

    public Task<int> CountAsync() => _repo.CountAsync();

    // Validate the file's leading bytes against its claimed extension. Text formats (.txt/.md) have no
    // reliable signature, so they pass; binary formats must match a known magic-byte signature.
    // Returns (matches, detectedLabel) so the caller can report what was expected vs. what arrived.
    private static async Task<(bool Matches, string Detected)> ContentMatchesExtensionAsync(Stream stream, string ext)
    {
        if (ext is ".txt" or ".md") return (true, "text");

        var header = new byte[12];
        stream.Position = 0;
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length));
        if (read < 4) return (false, "empty or truncated file");

        bool StartsWith(params byte[] sig) => header.Take(sig.Length).SequenceEqual(sig);

        var detected = DetectFormat(header);

        var matches = ext switch
        {
            ".jpg" or ".jpeg" => StartsWith(0xFF, 0xD8, 0xFF),
            ".png" => StartsWith(0x89, 0x50, 0x4E, 0x47),
            ".gif" => StartsWith(0x47, 0x49, 0x46, 0x38),
            ".webp" => header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                       && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50,
            ".pdf" => StartsWith(0x25, 0x50, 0x44, 0x46),
            ".doc" => StartsWith(0xD0, 0xCF, 0x11, 0xE0),
            ".docx" => StartsWith(0x50, 0x4B, 0x03, 0x04),
            _ => false
        };
        return (matches, detected);
    }

    // Best-effort identification of a file's real type from its leading bytes, for error messages.
    private static string DetectFormat(byte[] header)
    {
        bool At(int i, params byte[] sig) => header.Length >= i + sig.Length
            && header.Skip(i).Take(sig.Length).SequenceEqual(sig);

        if (At(0, 0xFF, 0xD8, 0xFF)) return "JPEG image";
        if (At(0, 0x89, 0x50, 0x4E, 0x47)) return "PNG image";
        if (At(0, 0x47, 0x49, 0x46, 0x38)) return "GIF image";
        if (At(0, 0x52, 0x49, 0x46, 0x46) && At(8, 0x57, 0x45, 0x42, 0x50)) return "WebP image";
        if (At(0, 0x25, 0x50, 0x44, 0x46)) return "PDF document";
        if (At(0, 0xD0, 0xCF, 0x11, 0xE0)) return "legacy Office document (.doc/.xls)";
        if (At(0, 0x50, 0x4B, 0x03, 0x04)) return "ZIP archive or .docx/.xlsx";
        if (At(0, 0x42, 0x4D)) return "BMP image";
        if (At(0, 0x49, 0x49, 0x2A, 0x00) || At(0, 0x4D, 0x4D, 0x00, 0x2A)) return "TIFF image";
        return "an unrecognized format";
    }

    // Human-readable name for the format an extension is supposed to carry (for error messages).
    private static string ExpectedLabel(string ext) => ext switch
    {
        ".jpg" or ".jpeg" => "JPEG image",
        ".png" => "PNG image",
        ".gif" => "GIF image",
        ".webp" => "WebP image",
        ".pdf" => "PDF document",
        ".doc" => "Word document",
        ".docx" => "Word (.docx) document",
        _ => $"{ext.TrimStart('.').ToUpperInvariant()} file"
    };

    private static string ResolveMime(string ext) => ext switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".txt" => "text/plain",
        ".md" => "text/markdown",
        _ => "application/octet-stream"
    };

    private static AssetDto ToDto(Asset a) => new(
        a.Id, a.FileName, a.FilePath, a.AssetType, a.MimeType, a.FileSize,
        a.Title, a.Description, a.Tags, a.UploadedByName ?? "Unknown", a.UploadedAt, a.UpdatedAt,
        a.SourceType, a.SourceId);
}
