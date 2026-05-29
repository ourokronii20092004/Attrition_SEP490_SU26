using Assets.Service.DTOs;
using Assets.Service.Services;
using BuildingBlocks.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Assets.Service.Controllers;

[ApiController]
[Route("api/assets")]
public class AssetsPublicController : ControllerBase
{
    private readonly IAssetService _assets;
    public AssetsPublicController(IAssetService assets) => _assets = assets;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? assetType = null, [FromQuery] string? search = null)
        => Ok(ApiResponse<PaginatedResponse<AssetDto>>.Ok(
            await _assets.ListAssetsAsync(page, pageSize, assetType, search)));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var asset = await _assets.GetAssetAsync(id);
        return asset == null
            ? NotFound(ApiResponse.Fail("Asset not found."))
            : Ok(ApiResponse<AssetDto>.Ok(asset));
    }
}
