using Attrition.API.DTOs;
using Attrition.API.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace Attrition.API.Controllers;

[ApiController]
[Route("api/assets")]
public class PublicAssetController : ControllerBase
{
    private readonly IAssetService _assetService;

    public PublicAssetController(IAssetService assetService)
    {
        _assetService = assetService;
    }

    [HttpGet]
    public async Task<IActionResult> ListAssets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? assetType = null,
        [FromQuery] string? search = null)
    {
        var result = await _assetService.ListAssetsAsync(page, pageSize, assetType, search);
        return Ok(new ApiResponse<PaginatedResponse<AssetDto>>(true, result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsset(Guid id)
    {
        var result = await _assetService.GetAssetAsync(id);
        if (result == null) return NotFound(new ApiResponse(false, "Asset not found."));
        return Ok(new ApiResponse<AssetDto>(true, result));
    }
}
