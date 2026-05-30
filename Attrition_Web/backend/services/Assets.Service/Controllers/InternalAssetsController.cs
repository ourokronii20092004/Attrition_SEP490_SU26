using Assets.Service.Services;
using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Assets.Service.Controllers;

/// <summary>Service-to-service lookups for the Admin stats aggregator. Guarded by X-Internal-Key.</summary>
[ApiController]
[Route("api/internal/assets")]
public class InternalAssetsController : ControllerBase
{
    private readonly IAssetService _assets;
    private readonly IConfiguration _config;

    public InternalAssetsController(IAssetService assets, IConfiguration config)
    {
        _assets = assets;
        _config = config;
    }

    private bool KeyValid() => InternalKey.Validate(Request, _config);

    [HttpGet("count")]
    public async Task<IActionResult> Count()
    {
        if (!KeyValid()) return Unauthorized();
        return Ok(ApiResponse<int>.Ok(await _assets.CountAsync()));
    }
}
