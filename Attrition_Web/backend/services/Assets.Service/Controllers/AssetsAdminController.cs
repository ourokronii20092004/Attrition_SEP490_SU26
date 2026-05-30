using Assets.Service.DTOs;
using Assets.Service.Services;
using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Assets.Service.Controllers;

[ApiController]
[Route("api/admin/assets")]
[Authorize(Roles = Roles.Admin)]
public class AssetsAdminController : ControllerBase
{
    private readonly IAssetService _assets;
    private readonly ICurrentUser _user;

    public AssetsAdminController(IAssetService assets, ICurrentUser user)
    {
        _assets = assets;
        _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? assetType = null, [FromQuery] string? search = null)
        => Ok(ApiResponse<PaginatedResponse<AssetDto>>.Ok(
            await _assets.ListAssetsAsync(page, pageSize, assetType, search)));

    [HttpPost]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile file,
        [FromForm] string assetType,
        [FromForm] string? title = null,
        [FromForm] string? description = null,
        [FromForm] string? tags = null)
    {
        var result = await _assets.UploadAssetAsync(file, assetType, title, description, tags,
            _user.UserId!.Value, _user.Username ?? "Unknown");
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAssetReq req)
    {
        var result = await _assets.UpdateAssetAsync(id, req);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _assets.DeleteAssetAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
