using BuildingBlocks.Contracts;
using Enemy.Service.DTOs;

namespace Enemy.Service.Services.Interface;

public interface IItemService
{
    Task<List<ItemResponse>> GetAllAsync(string? category, string? search);

    Task<ItemResponse?> GetByIdAsync(string itemId);

    Task<ApiResponse<ItemResponse>> CreateAsync(ItemCreateRequest request);

    Task<ApiResponse<ItemResponse>> UpdateAsync(string itemId, ItemUpdateRequest request);

    Task<ApiResponse> DeleteAsync(string itemId);

    /// <summary>Cục item config gộp (item + modifiers) cho game tải 1 lần, kèm version.</summary>
    Task<ItemConfigBundle> GetConfigBundleAsync();

    /// <summary>(version, count) toàn bảng item — để gộp version chung với enemy.</summary>
    Task<(string version, int count)> GetVersionInfoAsync();
}