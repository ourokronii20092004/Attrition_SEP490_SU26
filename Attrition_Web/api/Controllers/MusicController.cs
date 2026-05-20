namespace Attrition.API.Controllers;

using Attrition.API.Data;
using Attrition.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[ApiController]
[Route("api/music")]
public class MusicController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public MusicController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    private string GetUploadPath() => _config["FileUpload:UploadPath"] ?? "./uploads";

    // ─── Albums (Public) ───

    [HttpGet("albums")]
    public async Task<IActionResult> GetAlbums()
    {
        var albums = await _db.MusicAlbums
            .OrderBy(a => a.SortOrder)
            .Select(a => new
            {
                a.AlbumId, a.Title, a.Slug, a.Artist, a.Description,
                a.CoverPath, a.ReleaseDate, a.AlbumType,
                a.TrackCount, a.TotalDuration, a.CreatedAt
            })
            .ToListAsync();
        return Ok(new { success = true, data = albums });
    }

    [HttpGet("albums/{id}")]
    public async Task<IActionResult> GetAlbum(int id)
    {
        var album = await _db.MusicAlbums
            .Include(a => a.Tracks.OrderBy(t => t.TrackNumber))
            .FirstOrDefaultAsync(a => a.AlbumId == id);
        if (album == null) return NotFound(new { success = false, error = "Album not found" });

        return Ok(new { success = true, data = album });
    }

    // ─── Tracks (Public) ───

    [HttpGet("tracks")]
    public async Task<IActionResult> GetTracks([FromQuery] int? albumId)
    {
        var query = _db.MusicTracks.AsQueryable();
        if (albumId.HasValue) query = query.Where(t => t.AlbumId == albumId.Value);

        var tracks = await query
            .OrderBy(t => t.AlbumId).ThenBy(t => t.TrackNumber)
            .Select(t => new
            {
                t.TrackId, t.AlbumId, t.Title, t.Slug, t.TrackNumber,
                t.Duration, t.Genre, t.PlayCount, t.IsFeatured, t.FileSize,
                AlbumTitle = t.Album.Title, AlbumCoverPath = t.Album.CoverPath
            })
            .ToListAsync();
        return Ok(new { success = true, data = tracks });
    }

    [HttpGet("tracks/featured")]
    public async Task<IActionResult> GetFeaturedTracks()
    {
        var tracks = await _db.MusicTracks
            .Where(t => t.IsFeatured)
            .OrderByDescending(t => t.PlayCount)
            .Select(t => new
            {
                t.TrackId, t.AlbumId, t.Title, t.Slug, t.TrackNumber,
                t.Duration, t.Genre, t.PlayCount, t.FileSize,
                AlbumTitle = t.Album.Title, AlbumCoverPath = t.Album.CoverPath
            })
            .ToListAsync();
        return Ok(new { success = true, data = tracks });
    }

    [HttpGet("tracks/{id}/stream")]
    public async Task<IActionResult> StreamTrack(int id)
    {
        var track = await _db.MusicTracks.FindAsync(id);
        if (track == null) return NotFound();

        var filePath = Path.Combine(GetUploadPath(), "music", track.FilePath);
        if (!System.IO.File.Exists(filePath)) return NotFound();

        return PhysicalFile(filePath, "audio/mpeg", enableRangeProcessing: true);
    }

    [HttpPost("tracks/{id}/play")]
    public async Task<IActionResult> IncrementPlayCount(int id)
    {
        var track = await _db.MusicTracks.FindAsync(id);
        if (track == null) return NotFound();

        track.PlayCount++;
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ─── Favorites (Authenticated) ───

    [HttpGet("favorites")]
    [Authorize]
    public async Task<IActionResult> GetFavorites()
    {
        var userId = Guid.Parse(User.FindFirstValue("sub")!);
        var favorites = await _db.UserFavorites
            .Where(f => f.UserId == userId)
            .Include(f => f.Track)
                .ThenInclude(t => t.Album)
            .OrderByDescending(f => f.AddedAt)
            .Select(f => new
            {
                f.Track.TrackId, f.Track.AlbumId, f.Track.Title, f.Track.Slug,
                f.Track.TrackNumber, f.Track.Duration, f.Track.Genre, f.Track.PlayCount,
                AlbumTitle = f.Track.Album.Title, AlbumCoverPath = f.Track.Album.CoverPath,
                FavoritedAt = f.AddedAt
            })
            .ToListAsync();
        return Ok(new { success = true, data = favorites });
    }

    [HttpPost("favorites/{trackId}")]
    [Authorize]
    public async Task<IActionResult> ToggleFavorite(int trackId)
    {
        var userId = Guid.Parse(User.FindFirstValue("sub")!);
        var existing = await _db.UserFavorites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.TrackId == trackId);

        if (existing != null)
        {
            _db.UserFavorites.Remove(existing);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, data = new { isFavorited = false } });
        }

        var track = await _db.MusicTracks.FindAsync(trackId);
        if (track == null) return NotFound(new { success = false, error = "Track not found" });

        _db.UserFavorites.Add(new UserFavorite { UserId = userId, TrackId = trackId });
        await _db.SaveChangesAsync();
        return Ok(new { success = true, data = new { isFavorited = true } });
    }

    // ─── Admin: Album CRUD ───

    [HttpPost("albums")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateAlbum([FromBody] CreateAlbumRequest req)
    {
        var album = new MusicAlbum
        {
            Title = req.Title,
            Slug = req.Title.ToLower().Replace(" ", "-").Replace("'", ""),
            Artist = req.Artist ?? "Attrition OST",
            Description = req.Description,
            AlbumType = req.AlbumType ?? "soundtrack",
            ReleaseDate = req.ReleaseDate,
            SortOrder = req.SortOrder
        };

        _db.MusicAlbums.Add(album);
        await _db.SaveChangesAsync();
        return Ok(new { success = true, data = album });
    }

    [HttpPut("albums/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateAlbum(int id, [FromBody] CreateAlbumRequest req)
    {
        var album = await _db.MusicAlbums.FindAsync(id);
        if (album == null) return NotFound(new { success = false, error = "Album not found" });

        album.Title = req.Title;
        album.Slug = req.Title.ToLower().Replace(" ", "-").Replace("'", "");
        album.Artist = req.Artist ?? album.Artist;
        album.Description = req.Description;
        album.AlbumType = req.AlbumType ?? album.AlbumType;
        album.ReleaseDate = req.ReleaseDate;
        album.SortOrder = req.SortOrder;

        await _db.SaveChangesAsync();
        return Ok(new { success = true, data = album });
    }

    [HttpDelete("albums/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAlbum(int id)
    {
        var album = await _db.MusicAlbums.Include(a => a.Tracks).FirstOrDefaultAsync(a => a.AlbumId == id);
        if (album == null) return NotFound(new { success = false, error = "Album not found" });

        // Delete track files
        foreach (var track in album.Tracks)
        {
            var filePath = Path.Combine(GetUploadPath(), "music", track.FilePath);
            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
        }

        // Delete cover
        if (!string.IsNullOrEmpty(album.CoverPath))
        {
            var coverPath = Path.Combine(GetUploadPath(), album.CoverPath.TrimStart('/'));
            if (System.IO.File.Exists(coverPath)) System.IO.File.Delete(coverPath);
        }

        _db.MusicAlbums.Remove(album);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpPost("albums/{id}/cover")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UploadAlbumCover(int id, IFormFile file)
    {
        var album = await _db.MusicAlbums.FindAsync(id);
        if (album == null) return NotFound(new { success = false, error = "Album not found" });

        if (file.Length > 10 * 1024 * 1024) // 10MB limit for covers
            return BadRequest(new { success = false, error = "Cover image must be under 10MB" });

        var coverDir = Path.Combine(GetUploadPath(), "music", "covers");
        Directory.CreateDirectory(coverDir);

        var ext = Path.GetExtension(file.FileName);
        var fileName = $"album-{id}{ext}";
        var filePath = Path.Combine(coverDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        album.CoverPath = $"/uploads/music/covers/{fileName}";
        await _db.SaveChangesAsync();

        return Ok(new { success = true, data = new { coverPath = album.CoverPath } });
    }

    // ─── Admin: Track CRUD ───

    [HttpPost("tracks")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UploadTrack([FromForm] UploadTrackRequest req)
    {
        var album = await _db.MusicAlbums.FindAsync(req.AlbumId);
        if (album == null) return BadRequest(new { success = false, error = "Album not found" });

        if (req.File == null || req.File.Length == 0)
            return BadRequest(new { success = false, error = "Audio file is required" });

        if (req.File.Length > 100 * 1024 * 1024) // 100MB limit
            return BadRequest(new { success = false, error = "File must be under 100MB" });

        // Save file
        var trackDir = Path.Combine(GetUploadPath(), "music", "tracks");
        Directory.CreateDirectory(trackDir);

        var slug = req.Title.ToLower().Replace(" ", "-").Replace("'", "");
        var ext = Path.GetExtension(req.File.FileName);
        var fileName = $"{req.TrackNumber:D2}-{slug}{ext}";
        var filePath = Path.Combine(trackDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
            await req.File.CopyToAsync(stream);

        var track = new MusicTrack
        {
            AlbumId = req.AlbumId,
            Title = req.Title,
            Slug = slug,
            TrackNumber = req.TrackNumber,
            Duration = req.Duration,
            FilePath = $"tracks/{fileName}",
            FileSize = req.File.Length,
            Genre = req.Genre,
            IsFeatured = req.IsFeatured
        };

        _db.MusicTracks.Add(track);

        // Update album stats
        album.TrackCount = await _db.MusicTracks.CountAsync(t => t.AlbumId == req.AlbumId) + 1;
        album.TotalDuration = await _db.MusicTracks.Where(t => t.AlbumId == req.AlbumId).SumAsync(t => t.Duration) + req.Duration;

        await _db.SaveChangesAsync();
        return Ok(new { success = true, data = track });
    }

    [HttpPut("tracks/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateTrack(int id, [FromBody] UpdateTrackRequest req)
    {
        var track = await _db.MusicTracks.FindAsync(id);
        if (track == null) return NotFound(new { success = false, error = "Track not found" });

        track.Title = req.Title ?? track.Title;
        track.TrackNumber = req.TrackNumber ?? track.TrackNumber;
        track.Duration = req.Duration ?? track.Duration;
        track.Genre = req.Genre ?? track.Genre;
        track.IsFeatured = req.IsFeatured ?? track.IsFeatured;

        await _db.SaveChangesAsync();
        return Ok(new { success = true, data = track });
    }

    [HttpDelete("tracks/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteTrack(int id)
    {
        var track = await _db.MusicTracks.FindAsync(id);
        if (track == null) return NotFound(new { success = false, error = "Track not found" });

        // Delete file
        var filePath = Path.Combine(GetUploadPath(), "music", track.FilePath);
        if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);

        var albumId = track.AlbumId;
        _db.MusicTracks.Remove(track);
        await _db.SaveChangesAsync();

        // Update album stats
        var album = await _db.MusicAlbums.FindAsync(albumId);
        if (album != null)
        {
            album.TrackCount = await _db.MusicTracks.CountAsync(t => t.AlbumId == albumId);
            album.TotalDuration = await _db.MusicTracks.Where(t => t.AlbumId == albumId).SumAsync(t => t.Duration);
            await _db.SaveChangesAsync();
        }

        return Ok(new { success = true });
    }
}

// ─── Request DTOs ───

public record CreateAlbumRequest(
    string Title,
    string? Artist,
    string? Description,
    string? AlbumType,
    DateTime? ReleaseDate,
    int SortOrder = 0
);

public class UploadTrackRequest
{
    public int AlbumId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TrackNumber { get; set; }
    public int Duration { get; set; }
    public string? Genre { get; set; }
    public bool IsFeatured { get; set; }
    public IFormFile? File { get; set; }
}

public record UpdateTrackRequest(
    string? Title,
    int? TrackNumber,
    int? Duration,
    string? Genre,
    bool? IsFeatured
);