using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Music.Service.DTOs;
using Music.Service.Models;
using Music.Service.Services;

namespace Music.Service.Controllers;

[ApiController]
[Route("api/music")]
public class MusicController : ControllerBase
{
    private readonly IAlbumService _albums;
    private readonly ITrackService _tracks;
    private readonly IFavoriteService _favorites;
    private readonly IPlaylistService _playlists;
    private readonly ICurrentUser _user;

    public MusicController(IAlbumService albums, ITrackService tracks, IFavoriteService favorites,
        IPlaylistService playlists, ICurrentUser user)
    {
        _albums = albums;
        _tracks = tracks;
        _favorites = favorites;
        _playlists = playlists;
        _user = user;
    }

    // ─── Albums (public) ───
    [HttpGet("albums")]
    public async Task<IActionResult> GetAlbums([FromQuery] int? page, [FromQuery] int pageSize = 24)
    {
        // Paged when ?page is supplied; otherwise the full list (back-compat for admin dropdowns).
        if (page is { } p)
            return Ok(ApiResponse<PaginatedResponse<MusicAlbumDto>>.Ok(
                await _albums.GetAlbumsPagedAsync(p < 1 ? 1 : p, pageSize)));
        return Ok(ApiResponse<IEnumerable<MusicAlbumDto>>.Ok(await _albums.GetAlbumsAsync()));
    }

    [HttpGet("albums/{id:int}")]
    public async Task<IActionResult> GetAlbum(int id)
    {
        var result = await _albums.GetAlbumAsync(id);
        return result != null ? Ok(ApiResponse<AlbumDetailDto>.Ok(result)) : NotFound(ApiResponse.Fail("Album not found"));
    }

    // ─── Tracks (public) ───
    [HttpGet("tracks")]
    public async Task<IActionResult> GetTracks([FromQuery] int? albumId, [FromQuery] int? page, [FromQuery] int pageSize = 50)
    {
        // Paged when ?page is supplied; otherwise the full list (back-compat for album track lists).
        if (page is { } p)
            return Ok(ApiResponse<PaginatedResponse<MusicTrackDto>>.Ok(
                await _tracks.GetTracksPagedAsync(albumId, p < 1 ? 1 : p, pageSize)));
        return Ok(ApiResponse<IEnumerable<MusicTrackDto>>.Ok(await _tracks.GetTracksAsync(albumId)));
    }

    [HttpGet("tracks/featured")]
    public async Task<IActionResult> GetFeatured()
        => Ok(ApiResponse<FeaturedTracksResponse>.Ok(await _tracks.GetFeaturedTracksAsync()));

    [HttpGet("tracks/{id:int}/stream")]
    public async Task<IActionResult> Stream(int id)
    {
        // Intentionally public — see docs/design-choices/README.md (DC-01).
        var (filePath, trackExists) = await _tracks.GetTrackStreamInfoAsync(id);
        if (!trackExists) return NotFound(ApiResponse.Fail("Track not found"));
        if (filePath == null) return NotFound(ApiResponse.Fail("Track audio file not found on disk"));
        BuildingBlocks.Web.MediaSecurityHeaders.Apply(Response);
        return PhysicalFile(filePath, MusicHelpers.GetAudioContentType(filePath), enableRangeProcessing: true);
    }

    [HttpGet("tracks/{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var (filePath, fileName, trackExists) = await _tracks.GetTrackDownloadInfoAsync(id);
        if (!trackExists) return NotFound(ApiResponse.Fail("Track not found"));
        if (filePath == null) return NotFound(ApiResponse.Fail("Track audio file not found on disk"));
        // fileDownloadName sets Content-Disposition: attachment so the browser saves it.
        BuildingBlocks.Web.MediaSecurityHeaders.Apply(Response);
        return PhysicalFile(filePath, "application/octet-stream", fileName);
    }

    [Authorize]
    [HttpPost("tracks/{id:int}/play")]
    public async Task<IActionResult> IncrementPlay(int id)
    {
        var ok = await _tracks.IncrementPlayCountAsync(id);
        return ok ? Ok(ApiResponse.Ok()) : NotFound(ApiResponse.Fail("Track not found"));
    }

    // ─── Favorites (authenticated) ───
    [Authorize]
    [HttpGet("favorites")]
    public async Task<IActionResult> GetFavorites()
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        return Ok(ApiResponse<IEnumerable<FavoriteTrackDto>>.Ok(await _favorites.GetFavoritesAsync(userId)));
    }

    [Authorize]
    [HttpGet("favorites/ids")]
    public async Task<IActionResult> GetFavoriteIds()
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        return Ok(ApiResponse<IEnumerable<int>>.Ok(await _favorites.GetFavoriteIdsAsync(userId)));
    }

    [Authorize]
    [HttpPost("favorites/{trackId:int}")]
    public async Task<IActionResult> ToggleFavorite(int trackId)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var (success, isFavorited, err) = await _favorites.ToggleFavoriteAsync(userId, trackId);
        return success ? Ok(ApiResponse<object>.Ok(new { isFavorited })) : NotFound(ApiResponse.Fail(err!));
    }

    // ─── Playlists (authenticated) ───
    [Authorize]
    [HttpGet("playlists")]
    public async Task<IActionResult> GetPlaylists()
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        return Ok(ApiResponse<IEnumerable<PlaylistDto>>.Ok(await _playlists.GetPlaylistsAsync(userId)));
    }

    [Authorize]
    [HttpPost("playlists")]
    public async Task<IActionResult> CreatePlaylist([FromBody] CreatePlaylistReq req)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        return Ok(ApiResponse<PlaylistDto>.Ok(await _playlists.CreatePlaylistAsync(userId, req.Name, req.Description)));
    }

    [Authorize]
    [HttpPost("playlists/{id:guid}/tracks")]
    public async Task<IActionResult> AddTrack(Guid id, [FromBody] AddTrackToPlaylistReq req)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _playlists.AddTrackToPlaylistAsync(userId, id, req.TrackId);
        return result switch
        {
            PlaylistOpResult.Ok => Ok(ApiResponse.Ok()),
            PlaylistOpResult.NotFound => NotFound(ApiResponse.Fail("Playlist not found.")),
            _ => StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("You do not own this playlist."))
        };
    }

    [Authorize]
    [HttpDelete("playlists/{id:guid}/tracks/{trackId:int}")]
    public async Task<IActionResult> RemoveTrack(Guid id, int trackId)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _playlists.RemoveTrackFromPlaylistAsync(userId, id, trackId);
        return result switch
        {
            PlaylistOpResult.Ok => Ok(ApiResponse.Ok()),
            PlaylistOpResult.NotFound => NotFound(ApiResponse.Fail("Playlist or track not found.")),
            _ => StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("You do not own this playlist."))
        };
    }

    // ─── Admin: albums ───
    [Authorize(Roles = Roles.Admin)]
    [HttpPost("albums")]
    public async Task<IActionResult> CreateAlbum([FromBody] CreateAlbumRequest req)
        => Ok(ApiResponse<MusicAlbumDto>.Ok(await _albums.CreateAlbumAsync(req)));

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("albums/{id:int}")]
    public async Task<IActionResult> UpdateAlbum(int id, [FromBody] CreateAlbumRequest req)
    {
        var result = await _albums.UpdateAlbumAsync(id, req);
        return result != null ? Ok(ApiResponse<MusicAlbumDto>.Ok(result)) : NotFound(ApiResponse.Fail("Album not found"));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("albums/{id:int}")]
    public async Task<IActionResult> DeleteAlbum(int id)
    {
        var ok = await _albums.DeleteAlbumAsync(id);
        return ok ? Ok(ApiResponse.Ok()) : NotFound(ApiResponse.Fail("Album not found"));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost("albums/{id:int}/cover")]
    public async Task<IActionResult> UploadCover(int id, IFormFile? file)
    {
        if (file is null) return BadRequest(ApiResponse.Fail("No cover image file was provided."));
        var (success, error, coverPath) = await _albums.UploadAlbumCoverAsync(id, file);
        if (!success) return error == "Album not found" ? NotFound(ApiResponse.Fail(error)) : BadRequest(ApiResponse.Fail(error!));
        return Ok(ApiResponse<object>.Ok(new { coverPath }));
    }

    // ─── Admin: tracks ───
    [Authorize(Roles = Roles.Admin)]
    [HttpPost("tracks/scan")]
    public async Task<IActionResult> ScanTrack(IFormFile? file)
    {
        if (file is null) return BadRequest(ApiResponse.Fail("No audio file was provided."));
        var (success, error, data) = await _tracks.ScanTrackAsync(file);
        return success ? Ok(ApiResponse<ScanTrackResponse>.Ok(data!)) : BadRequest(ApiResponse.Fail(error!));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost("tracks")]
    public async Task<IActionResult> UploadTrack([FromForm] UploadTrackRequest req)
    {
        var (success, error, data) = await _tracks.UploadTrackAsync(req);
        return success ? Ok(ApiResponse<MusicTrackDto>.Ok(data!)) : BadRequest(ApiResponse.Fail(error!));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("tracks/{id:int}")]
    public async Task<IActionResult> UpdateTrack(int id, [FromBody] UpdateTrackRequest req)
    {
        var (success, error, data) = await _tracks.UpdateTrackAsync(id, req);
        return success ? Ok(ApiResponse<MusicTrackDto>.Ok(data!)) : NotFound(ApiResponse.Fail(error!));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("tracks/{id:int}")]
    public async Task<IActionResult> DeleteTrack(int id)
    {
        var ok = await _tracks.DeleteTrackAsync(id);
        return ok ? Ok(ApiResponse.Ok()) : NotFound(ApiResponse.Fail("Track not found"));
    }
}
