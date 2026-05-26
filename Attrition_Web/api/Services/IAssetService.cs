using Attrition.API.DTOs;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Attrition.API.Services;

public interface IAssetService
{
    Task<AssetDto?> GetAssetAsync(Guid assetId);
    Task<PaginatedResponse<AssetDto>> ListAssetsAsync(int page, int pageSize, string? assetType, string? search);
    Task<ApiResponse<AssetDto>> UploadAssetAsync(IFormFile file, string assetType, string? title, string? description, string? tags, Guid userId);
    Task<ApiResponse> UpdateAssetAsync(Guid assetId, UpdateAssetReq req);
    Task<ApiResponse> DeleteAssetAsync(Guid assetId);
}
