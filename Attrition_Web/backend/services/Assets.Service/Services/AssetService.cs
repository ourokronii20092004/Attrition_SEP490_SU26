using System.Linq.Expressions;
using Assets.Service.DTOs;
using Assets.Service.Models;
using Assets.Service.Repositories;
using BuildingBlocks.Contracts;
using Microsoft.AspNetCore.Http;

namespace Assets.Service.Services;

public class AssetService : IAssetService
{
    private static readonly string[] ImageExts = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private static readonly string[] DocExts = { ".pdf", ".doc", ".docx", ".txt", ".md" };

    private readonly IAssetRepository _repo;
    private readonly IFileStorage _storage;
    private readonly long _maxSize;

    public AssetService(IAssetRepository repo, IFileStorage storage, IConfiguration config)
    {
        _repo = repo;
        _storage = storage;
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
        string? title, string? description, string? tags, Guid userId, string userName)
    {
        if (file == null || file.Length == 0)
            return ApiResponse<AssetDto>.Fail("File is empty.");
        if (file.Length > _maxSize)
            return ApiResponse<AssetDto>.Fail($"File exceeds the maximum allowed size of {_maxSize / (1024 * 1024)}MB.");

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
            if (!await ContentMatchesExtensionAsync(stream, ext))
                return ApiResponse<AssetDto>.Fail("File content does not match its extension.");
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
            UploadedById = userId,
            UploadedByName = userName
        };
        await _repo.AddAsync(asset);

        return ApiResponse<AssetDto>.Ok(ToDto(asset));
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
    private static async Task<bool> ContentMatchesExtensionAsync(Stream stream, string ext)
    {
        if (ext is ".txt" or ".md") return true;

        var header = new byte[12];
        stream.Position = 0;
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length));
        if (read < 4) return false;

        bool StartsWith(params byte[] sig) => header.Take(sig.Length).SequenceEqual(sig);

        return ext switch
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
    }

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
        a.Title, a.Description, a.Tags, a.UploadedByName ?? "Unknown", a.UploadedAt, a.UpdatedAt);
}
