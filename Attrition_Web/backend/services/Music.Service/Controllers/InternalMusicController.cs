using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Microsoft.AspNetCore.Mvc;
using Music.Service.Services;

namespace Music.Service.Controllers;

/// <summary>Service-to-service lookups for the Admin stats aggregator. Guarded by X-Internal-Key.</summary>
[ApiController]
[Route("api/internal/music")]
public class InternalMusicController : ControllerBase
{
    private readonly IAlbumService _albums;
    private readonly ITrackService _tracks;
    private readonly IConfiguration _config;

    public InternalMusicController(IAlbumService albums, ITrackService tracks, IConfiguration config)
    {
        _albums = albums;
        _tracks = tracks;
        _config = config;
    }

    private bool KeyValid() => InternalKey.Validate(Request, _config);

    [HttpGet("count")]
    public async Task<IActionResult> Count()
    {
        if (!KeyValid()) return Unauthorized(ApiResponse.Fail("Valid service authentication is required."));
        var albums = await _albums.CountAsync();
        var tracks = await _tracks.CountAsync();
        return Ok(ApiResponse<object>.Ok(new { albums, tracks }));
    }
}
