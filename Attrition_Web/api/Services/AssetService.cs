using Attrition.API.DTOs;
using Attrition.API.Models;
using Attrition.API.Repositories;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Attrition.API.Services;

public class AssetService : IAssetService
{
    private readonly IRepository<Asset> _assetRepo;
    private readonly IUserRepository _userRepo;
    private readonly IFileService _fileService;
    private readonly IFileStorage _storage;

    public AssetService(
        IRepository<Asset> assetRepo,
        IUserRepository userRepo,
        IFileService fileService,
        IFileStorage storage)
    {
        _assetRepo = assetRepo;
        _userRepo = userRepo;
        _fileService = fileService;
        _storage = storage;
    }

    public async Task<AssetDto?> GetAssetAsync(Guid assetId)
    {
        var a = await _assetRepo.GetByIdAsync(assetId);
        if (a == null) return null;
        var user = a.UploadedById.HasValue ? await _userRepo.GetByIdAsync(a.UploadedById.Value) : null;
        return new AssetDto(
            a.Id, a.FileName, a.FilePath, a.AssetType, a.MimeType, a.FileSize,
            a.Title, a.Description, a.Tags, user?.Username ?? "Unknown",
            a.UploadedAt, a.UpdatedAt
        );
    }

    public async Task<PaginatedResponse<AssetDto>> ListAssetsAsync(int page, int pageSize, string? assetType, string? search)
    {
        Expression<Func<Asset, bool>>? filter = null;

        if (!string.IsNullOrEmpty(assetType) && !string.IsNullOrEmpty(search))
        {
            filter = a => a.AssetType == assetType && a.FileName.ToLower().Contains(search.ToLower());
        }
        else if (!string.IsNullOrEmpty(assetType))
        {
            filter = a => a.AssetType == assetType;
        }
        else if (!string.IsNullOrEmpty(search))
        {
            filter = a => a.FileName.ToLower().Contains(search.ToLower());
        }

        var (items, totalCount) = await _assetRepo.GetPagedAsync(
            page, pageSize, filter,
            q => q.OrderByDescending(a => a.UploadedAt)
        );

        var dtos = new List<AssetDto>();
        foreach (var a in items)
        {
            var user = a.UploadedById.HasValue ? await _userRepo.GetByIdAsync(a.UploadedById.Value) : null;
            dtos.Add(new AssetDto(
                a.Id,
                a.FileName,
                a.FilePath,
                a.AssetType,
                a.MimeType,
                a.FileSize,
                a.Title,
                a.Description,
                a.Tags,
                user?.Username ?? "Unknown",
                a.UploadedAt,
                a.UpdatedAt
            ));
        }

        return new PaginatedResponse<AssetDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<ApiResponse<AssetDto>> UploadAssetAsync(IFormFile file, string assetType, string? title, string? description, string? tags, Guid userId)
    {
        if (file == null || file.Length == 0)
            return new ApiResponse<AssetDto>(false, Error: "File is empty.");

        string[] allowedExtensions;
        string subfolder;

        if (assetType == "document")
        {
            allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            subfolder = "sprites";
        }
        else
        {
            allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            subfolder = "assets";
            assetType = "image"; // normal image
        }

        var uploadResult = await _fileService.UploadGenericFileAsync(file, subfolder, allowedExtensions, 20 * 1024 * 1024);
        if (!uploadResult.Success)
            return new ApiResponse<AssetDto>(false, Error: uploadResult.Error);

        var asset = new Asset
        {
            FileName = file.FileName,
            FilePath = uploadResult.Data!,
            AssetType = assetType,
            MimeType = file.ContentType,
            FileSize = file.Length,
            Title = title,
            Description = description,
            Tags = tags,
            UploadedById = userId
        };

        await _assetRepo.AddAsync(asset);

        var user = await _userRepo.GetByIdAsync(userId);
        var dto = new AssetDto(
            asset.Id,
            asset.FileName,
            asset.FilePath,
            asset.AssetType,
            asset.MimeType,
            asset.FileSize,
            asset.Title,
            asset.Description,
            asset.Tags,
            user?.Username ?? "Unknown",
            asset.UploadedAt,
            asset.UpdatedAt
        );

        return new ApiResponse<AssetDto>(true, dto);
    }

    public async Task<ApiResponse> DeleteAssetAsync(Guid assetId)
    {
        var asset = await _assetRepo.GetByIdAsync(assetId);
        if (asset == null)
            return new ApiResponse(false, "Asset not found.");

        await _storage.DeleteAsync(asset.FilePath);
        await _assetRepo.DeleteAsync(asset);

        return new ApiResponse(true);
    }

    public async Task<ApiResponse> UpdateAssetAsync(Guid assetId, UpdateAssetReq req)
    {
        var asset = await _assetRepo.GetByIdAsync(assetId);
        if (asset == null) return new ApiResponse(false, "Asset not found.");

        if (req.Title != null) asset.Title = req.Title;
        if (req.Description != null) asset.Description = req.Description;
        if (req.Tags != null) asset.Tags = req.Tags;
        if (req.AssetType != null) asset.AssetType = req.AssetType;

        asset.UpdatedAt = DateTime.UtcNow;
        await _assetRepo.UpdateAsync(asset);

        return new ApiResponse(true);
    }
}
