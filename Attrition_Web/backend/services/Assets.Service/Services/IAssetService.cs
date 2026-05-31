using Assets.Service.DTOs;
using BuildingBlocks.Contracts;

namespace Assets.Service.Services;

public interface IAssetService
{
    Task<AssetDto?> GetAssetAsync(Guid assetId);
    Task<PaginatedResponse<AssetDto>> ListAssetsAsync(int page, int pageSize, string? assetType, string? search);
    Task<ApiResponse<AssetDto>> UploadAssetAsync(Microsoft.AspNetCore.Http.IFormFile file, string assetType,
        string? title, string? description, string? tags, Guid userId, string userName);
    /// <summary>Lightweight image upload for inline use (forum posts): validates + stores, returns the
    /// public URL only — no Asset DB row, no gallery entry.</summary>
    Task<ApiResponse<string>> UploadInlineImageAsync(Microsoft.AspNetCore.Http.IFormFile file);
    Task<ApiResponse> UpdateAssetAsync(Guid assetId, UpdateAssetReq req);
    Task<ApiResponse> DeleteAssetAsync(Guid assetId);
    Task<int> CountAsync();
}
