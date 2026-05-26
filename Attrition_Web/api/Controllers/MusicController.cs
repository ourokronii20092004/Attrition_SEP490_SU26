using Attrition.API.DTOs;
using Attrition.API.Services;
using Attrition.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace Attrition.API.Controllers;

[ApiController]
[Route("api/music")]
public class MusicController : ControllerBase
{
    private readonly IAlbumService _albumService;
    private readonly ITrackService _trackService;
    private readonly IFavoriteService _favoriteService;
    private readonly IPlaylistService _playlistService;

    public MusicController(
        IAlbumService albumService,
        ITrackService trackService,
        IFavoriteService favoriteService,
        IPlaylistService playlistService)
    {
        _albumService = albumService;
        _trackService = trackService;
        _favoriteService = favoriteService;
        _playlistService = playlistService;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);

    // ─── Albums (Public) ───

    [HttpGet("albums")]
    public async Task<IActionResult> GetAlbums()
        => Ok(new ApiResponse<IEnumerable<MusicAlbumDto>>(true, await _albumService.GetAlbumsAsync()));

    [HttpGet("albums/{id}")]
    public async Task<IActionResult> GetAlbum(int id)
    {
        var result = await _albumService.GetAlbumAsync(id);
        return result != null ? Ok(new ApiResponse<AlbumDetailDto>(true, result)) : NotFound(new ApiResponse(false, "Album not found"));
    }

    // ─── Tracks (Public) ───

    [HttpGet("tracks")]
    public async Task<IActionResult> GetTracks([FromQuery] int? albumId)
        => Ok(new ApiResponse<IEnumerable<MusicTrackDto>>(true, await _trackService.GetTracksAsync(albumId)));

    [HttpGet("tracks/featured")]
    public async Task<IActionResult> GetFeaturedTracks()
        => Ok(new ApiResponse<FeaturedTracksResponse>(true, await _trackService.GetFeaturedTracksAsync()));

    [HttpGet("tracks/{id}/stream")]
    public async Task<IActionResult> StreamTrack(int id)
    {
        var (filePath, trackExists) = await _trackService.GetTrackStreamInfoAsync(id);
        
        if (!trackExists) return NotFound(new ApiResponse(false, "Track not found"));
        if (filePath == null) return NotFound(new ApiResponse(false, "Track audio file not found on disk"));

        return PhysicalFile(filePath, "audio/mpeg", enableRangeProcessing: true);
    }

    [HttpPost("tracks/{id}/play")]
    public async Task<IActionResult> IncrementPlayCount(int id)
    {
        var success = await _trackService.IncrementPlayCountAsync(id);
        return success ? Ok(new ApiResponse(true)) : NotFound(new ApiResponse(false, "Track not found"));
    }

    // ─── Favorites (Authenticated) ───

    [HttpGet("favorites")]
    [Authorize]
    public async Task<IActionResult> GetFavorites()
    {
        return Ok(new ApiResponse<IEnumerable<FavoriteTrackDto>>(true, await _favoriteService.GetFavoritesAsync(UserId)));
    }

    [HttpGet("favorites/ids")]
    [Authorize]
    public async Task<IActionResult> GetFavoriteIds()
    {
        return Ok(new ApiResponse<IEnumerable<int>>(true, await _favoriteService.GetFavoriteIdsAsync(UserId)));
    }

    [HttpPost("favorites/{trackId}")]
    [Authorize]
    public async Task<IActionResult> ToggleFavorite(int trackId)
    {
        var (success, isFavorited, error) = await _favoriteService.ToggleFavoriteAsync(UserId, trackId);
        
        if (!success) return NotFound(new ApiResponse(false, error));
        
        return Ok(new ApiResponse<object>(true, new { isFavorited }));
    }

    // ─── Playlists (Authenticated) ───
    [HttpGet("playlists")]
    [Authorize]
    public async Task<IActionResult> GetPlaylists()
    {
        var playlists = await _playlistService.GetPlaylistsAsync(UserId);
        return Ok(new ApiResponse<IEnumerable<MusicPlaylist>>(true, playlists));
    }

    [HttpPost("playlists")]
    [Authorize]
    public async Task<IActionResult> CreatePlaylist([FromBody] CreatePlaylistReq req)
    {
        var playlist = await _playlistService.CreatePlaylistAsync(UserId, req.Name, req.Description);
        return Ok(new ApiResponse<MusicPlaylist>(true, playlist));
    }

    [HttpPost("playlists/{id}/tracks")]
    [Authorize]
    public async Task<IActionResult> AddTrackToPlaylist(Guid id, [FromBody] AddTrackToPlaylistReq req)
    {
        var success = await _playlistService.AddTrackToPlaylistAsync(id, req.TrackId);
        return success ? Ok(new ApiResponse(true)) : BadRequest(new ApiResponse(false, "Failed to add track. Playlist might not exist or track is already in playlist."));
    }

    [HttpDelete("playlists/{id}/tracks/{trackId}")]
    [Authorize]
    public async Task<IActionResult> RemoveTrackFromPlaylist(Guid id, int trackId)
    {
        var success = await _playlistService.RemoveTrackFromPlaylistAsync(id, trackId);
        return success ? Ok(new ApiResponse(true)) : BadRequest(new ApiResponse(false, "Failed to remove track."));
    }

    // ─── Admin: Album CRUD ───

    [HttpPost("albums")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateAlbum([FromBody] CreateAlbumRequest req)
        => Ok(new ApiResponse<MusicAlbum>(true, await _albumService.CreateAlbumAsync(req)));

    [HttpPut("albums/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateAlbum(int id, [FromBody] CreateAlbumRequest req)
    {
        var result = await _albumService.UpdateAlbumAsync(id, req);
        return result != null ? Ok(new ApiResponse<MusicAlbum>(true, result)) : NotFound(new ApiResponse(false, "Album not found"));
    }

    [HttpDelete("albums/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAlbum(int id)
    {
        var deleted = await _albumService.DeleteAlbumAsync(id);
        return deleted ? Ok(new ApiResponse(true)) : NotFound(new ApiResponse(false, "Album not found"));
    }

    [HttpPost("albums/{id}/cover")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UploadAlbumCover(int id, IFormFile file)
    {
        var (success, error, coverPath) = await _albumService.UploadAlbumCoverAsync(id, file);
        
        if (!success) 
        {
            if (error == "Album not found") return NotFound(new ApiResponse(false, error));
            return BadRequest(new ApiResponse(false, error));
        }
        
        return Ok(new ApiResponse<object>(true, new { coverPath }));
    }

    // ─── Admin: Track CRUD ───

    [HttpPost("tracks/scan")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ScanTrack(IFormFile file)
    {
        var (success, error, data) = await _trackService.ScanTrackAsync(file);
        if (!success) return BadRequest(new ApiResponse(false, error));
        
        return Ok(new ApiResponse<ScanTrackResponse>(true, data));
    }

    [HttpPost("tracks")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UploadTrack([FromForm] UploadTrackRequest req)
    {
        var (success, error, data) = await _trackService.UploadTrackAsync(req);
        if (!success) return BadRequest(new ApiResponse(false, error));
        
        return Ok(new ApiResponse<MusicTrackDto>(true, data));
    }

    [HttpPut("tracks/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateTrack(int id, [FromBody] UpdateTrackRequest req)
    {
        var (success, error, data) = await _trackService.UpdateTrackAsync(id, req);
        if (!success) return NotFound(new ApiResponse(false, error));
        
        return Ok(new ApiResponse<MusicTrackDto>(true, data));
    }

    [HttpDelete("tracks/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteTrack(int id)
    {
        var deleted = await _trackService.DeleteTrackAsync(id);
        return deleted ? Ok(new ApiResponse(true)) : NotFound(new ApiResponse(false, "Track not found"));
    }
}
