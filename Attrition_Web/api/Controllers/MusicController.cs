using Attrition.API.DTOs;
using Attrition.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Attrition.API.Controllers;

[ApiController]
[Route("api/music")]
public class MusicController : ControllerBase
{
    private readonly MusicService _music;

    public MusicController(MusicService music)
    {
        _music = music;
    }

    // ─── Albums (Public) ───

    [HttpGet("albums")]
    public async Task<IActionResult> GetAlbums()
        => Ok(await _music.GetAlbumsAsync());

    [HttpGet("albums/{id}")]
    public async Task<IActionResult> GetAlbum(int id)
    {
        var result = await _music.GetAlbumAsync(id);
        return result != null ? Ok(result) : NotFound(new { success = false, error = "Album not found" });
    }

    // ─── Tracks (Public) ───

    [HttpGet("tracks")]
    public async Task<IActionResult> GetTracks([FromQuery] int? albumId)
        => Ok(await _music.GetTracksAsync(albumId));

    [HttpGet("tracks/featured")]
    public async Task<IActionResult> GetFeaturedTracks()
        => Ok(await _music.GetFeaturedTracksAsync());

    [HttpGet("tracks/{id}/stream")]
    public async Task<IActionResult> StreamTrack(int id)
    {
        var (filePath, trackExists) = await _music.GetTrackStreamInfoAsync(id);
        
        if (!trackExists) return NotFound();
        if (filePath == null) return NotFound();

        return PhysicalFile(filePath, "audio/mpeg", enableRangeProcessing: true);
    }

    [HttpPost("tracks/{id}/play")]
    public async Task<IActionResult> IncrementPlayCount(int id)
    {
        var success = await _music.IncrementPlayCountAsync(id);
        return success ? Ok(new { success = true }) : NotFound();
    }

    // ─── Favorites (Authenticated) ───

    [HttpGet("favorites")]
    [Authorize]
    public async Task<IActionResult> GetFavorites()
    {
        var userId = Guid.Parse(User.FindFirstValue("sub")!);
        return Ok(await _music.GetFavoritesAsync(userId));
    }

    [HttpGet("favorites/ids")]
    [Authorize]
    public async Task<IActionResult> GetFavoriteIds()
    {
        var userId = Guid.Parse(User.FindFirstValue("sub")!);
        return Ok(await _music.GetFavoriteIdsAsync(userId));
    }

    [HttpPost("favorites/{trackId}")]
    [Authorize]
    public async Task<IActionResult> ToggleFavorite(int trackId)
    {
        var userId = Guid.Parse(User.FindFirstValue("sub")!);
        var (success, isFavorited, error) = await _music.ToggleFavoriteAsync(userId, trackId);
        
        if (!success) return NotFound(new { success = false, error });
        
        return Ok(new { success = true, data = new { isFavorited } });
    }

    // ─── Admin: Album CRUD ───

    [HttpPost("albums")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateAlbum([FromBody] CreateAlbumRequest req)
        => Ok(await _music.CreateAlbumAsync(req));

    [HttpPut("albums/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateAlbum(int id, [FromBody] CreateAlbumRequest req)
    {
        var result = await _music.UpdateAlbumAsync(id, req);
        return result != null ? Ok(result) : NotFound(new { success = false, error = "Album not found" });
    }

    [HttpDelete("albums/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAlbum(int id)
    {
        var deleted = await _music.DeleteAlbumAsync(id);
        return deleted ? Ok(new { success = true }) : NotFound(new { success = false, error = "Album not found" });
    }

    [HttpPost("albums/{id}/cover")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UploadAlbumCover(int id, IFormFile file)
    {
        var (success, error, coverPath) = await _music.UploadAlbumCoverAsync(id, file);
        
        if (!success) 
        {
            if (error == "Album not found") return NotFound(new { success = false, error });
            return BadRequest(new { success = false, error });
        }
        
        return Ok(new { success = true, data = new { coverPath } });
    }

    // ─── Admin: Track CRUD ───

    [HttpPost("tracks/scan")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ScanTrack(IFormFile file)
    {
        var (success, error, data) = await _music.ScanTrackAsync(file);
        if (!success) return BadRequest(new { success = false, error });
        
        return Ok(new { success = true, data });
    }

    [HttpPost("tracks")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UploadTrack([FromForm] UploadTrackRequest req)
    {
        var (success, error, data) = await _music.UploadTrackAsync(req);
        if (!success) return BadRequest(new { success = false, error });
        
        return Ok(new { success = true, data });
    }

    [HttpPut("tracks/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateTrack(int id, [FromBody] UpdateTrackRequest req)
    {
        var (success, error, data) = await _music.UpdateTrackAsync(id, req);
        if (!success) return NotFound(new { success = false, error });
        
        return Ok(new { success = true, data });
    }

    [HttpDelete("tracks/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteTrack(int id)
    {
        var deleted = await _music.DeleteTrackAsync(id);
        return deleted ? Ok(new { success = true }) : NotFound(new { success = false, error = "Track not found" });
    }
}
