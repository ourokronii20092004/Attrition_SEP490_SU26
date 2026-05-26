using Attrition.API.DTOs;
using Attrition.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace Attrition.API.Controllers;

[ApiController]
[Route("api/admin/assets")]
[Authorize(Roles = "Admin")]
public class AssetController : ControllerBase
{
    private readonly IAssetService _assetService;

    public AssetController(IAssetService assetService)
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

    [HttpPost]
    public async Task<IActionResult> UploadAsset(
        [FromForm] IFormFile file,
        [FromForm] string assetType,
        [FromForm] string? title = null,
        [FromForm] string? description = null,
        [FromForm] string? tags = null)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _assetService.UploadAssetAsync(file, assetType, title, description, tags, userId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsset(Guid id)
    {
        var result = await _assetService.DeleteAssetAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsset(Guid id, [FromBody] UpdateAssetReq req)
    {
        var result = await _assetService.UpdateAssetAsync(id, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
