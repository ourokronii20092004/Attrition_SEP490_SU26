using System.Linq.Expressions;
using BuildingBlocks.Caching;
using BuildingBlocks.Contracts;
using BuildingBlocks.Persistence;
using BuildingBlocks.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Music.Service.Data;
using Music.Service.DTOs;
using Music.Service.Models;

namespace Music.Service.Services;

public class TrackService : ITrackService
{
    private readonly IRepository<MusicAlbum> _albumRepo;
    private readonly IRepository<MusicTrack> _trackRepo;
    private readonly MusicDbContext _db;
    private readonly ICacheService _cache;
    private readonly ILogger<TrackService> _logger;
    private readonly string _uploadPath;
    private readonly string _publicPrefix;

    // Write-behind: count plays in Redis and only flush to Postgres every Nth play.
    // Durability tradeoff: Redis is an ephemeral cache (no persistence, allkeys-lru eviction),
    // so up to (PlayFlushEvery - 1) buffered plays per track can be lost on a Redis restart or
    // eviction. The counter also carries a 1-day TTL, so a track with fewer than PlayFlushEvery
    // plays in 24h may not flush until the next play tips it over. Accepted on purpose: play
    // counts are informational, not transactional. Lower this value to shrink the loss window.
    private const int PlayFlushEvery = 10;

    public TrackService(IRepository<MusicAlbum> albumRepo, IRepository<MusicTrack> trackRepo, MusicDbContext db,
        ICacheService cache, IConfiguration config, ILogger<TrackService> logger)
    {
        _albumRepo = albumRepo;
        _trackRepo = trackRepo;
        _db = db;
        _cache = cache;
        _logger = logger;
        _uploadPath = config["FileUpload:UploadPath"] ?? "/app/uploads";
        _publicPrefix = config["FileUpload:PublicPrefix"] ?? "/api/music/media";
    }

    private string MusicDir => Path.Combine(_uploadPath, "music");

    private async Task RescanAlbumAggregatesAsync(int albumId)
    {
        var album = await _albumRepo.GetByIdAsync(albumId);
        if (album == null) return;

        var tracks = await _trackRepo.ListAsync(t => t.AlbumId == albumId,
            q => q.OrderBy(t => t.TrackNumber));

        album.TrackCount = tracks.Count;
        album.TotalDuration = tracks.Sum(t => t.Duration);
        album.Genre = tracks.Select(t => t.Genre).Where(g => !string.IsNullOrEmpty(g)).Distinct().FirstOrDefault();
        album.Artists = tracks.SelectMany(t => t.Artists).Where(a => !string.IsNullOrEmpty(a)).Distinct().ToList();
        if (album.Artists.Count == 0) album.Artists = new List<string> { "Attrition OST" };

        if (!album.IsCoverUserDefined)
        {
            var firstWithCover = tracks.FirstOrDefault(t => !string.IsNullOrEmpty(t.CoverPath));
            album.CoverPath = firstWithCover?.CoverPath;
        }

        await _albumRepo.UpdateAsync(album);
    }

    public async Task<IEnumerable<MusicTrackDto>> GetTracksAsync(int? albumId)
    {
        Expression<Func<MusicTrack, bool>>? filter = albumId.HasValue ? t => t.AlbumId == albumId.Value : null;
        var tracks = await _trackRepo.ListAsync(filter,
            q => q.OrderBy(t => t.AlbumId).ThenBy(t => t.TrackNumber));

        var albumMap = await LoadAlbumsAsync(tracks.Select(t => t.AlbumId));
        return tracks.Select(t =>
        {
            albumMap.TryGetValue(t.AlbumId, out var album);
            return new MusicTrackDto(t.TrackId, t.AlbumId, t.Title, t.Slug, t.TrackNumber, t.Artists,
                t.Duration, t.Genre, t.CoverPath, t.PlayCount, t.IsFeatured, t.FileSize ?? 0, album?.Title, album?.CoverPath);
        }).ToList();
    }

    private async Task<Dictionary<int, MusicAlbum>> LoadAlbumsAsync(IEnumerable<int> albumIds)
    {
        var ids = albumIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, MusicAlbum>();
        var albums = await _albumRepo.ListAsync(a => ids.Contains(a.AlbumId));
        return albums.ToDictionary(a => a.AlbumId);
    }

    public async Task<PaginatedResponse<MusicTrackDto>> GetTracksPagedAsync(int? albumId, int page, int pageSize)
    {
        if (page < 1) page = 1;
        Expression<Func<MusicTrack, bool>>? filter = albumId.HasValue ? t => t.AlbumId == albumId.Value : null;
        var (tracks, total) = await _trackRepo.GetPagedAsync(page, pageSize, filter,
            q => q.OrderBy(t => t.AlbumId).ThenBy(t => t.TrackNumber));

        var albumMap = await LoadAlbumsAsync(tracks.Select(t => t.AlbumId));
        var items = tracks.Select(t =>
        {
            albumMap.TryGetValue(t.AlbumId, out var album);
            return new MusicTrackDto(t.TrackId, t.AlbumId, t.Title, t.Slug, t.TrackNumber, t.Artists,
                t.Duration, t.Genre, t.CoverPath, t.PlayCount, t.IsFeatured, t.FileSize ?? 0, album?.Title, album?.CoverPath);
        }).ToList();
        return new PaginatedResponse<MusicTrackDto>(items, total, page, pageSize);
    }

    public async Task<FeaturedTracksResponse> GetFeaturedTracksAsync()
    {
        var lastMonth = DateTime.UtcNow.AddDays(-30);
        var (featured, _) = await _trackRepo.GetPagedAsync(1, 10, t => t.CreatedAt >= lastMonth,
            q => q.OrderByDescending(t => t.PlayCount));

        if (featured.Count < 10)
        {
            var excluded = featured.Select(t => t.TrackId).ToList();
            var (fallback, _) = await _trackRepo.GetPagedAsync(1, 10 - featured.Count,
                t => !excluded.Contains(t.TrackId), q => q.OrderByDescending(t => t.PlayCount));
            featured.AddRange(fallback);
        }

        var sorted = featured.OrderByDescending(t => t.PlayCount).ToList();
        var albumMap = await LoadAlbumsAsync(sorted.Select(t => t.AlbumId));
        var featuredDtos = sorted.Select(t =>
        {
            albumMap.TryGetValue(t.AlbumId, out var album);
            return new MusicTrackDto(t.TrackId, t.AlbumId, t.Title, t.Slug, t.TrackNumber, t.Artists,
                t.Duration, t.Genre, t.CoverPath, t.PlayCount, t.IsFeatured, t.FileSize ?? 0, album?.Title, album?.CoverPath);
        }).ToList();

        // Newest albums: compute per-album track count + most-recent track date in the database,
        // take the top 5, then load only those albums. Avoids pulling every track/album into memory.
        var topAlbumStats = await _db.MusicTracks
            .GroupBy(t => t.AlbumId)
            .Select(g => new { AlbumId = g.Key, TrackCount = g.Count(), NewestTrackAddedAt = g.Max(t => t.CreatedAt) })
            .OrderByDescending(x => x.NewestTrackAddedAt)
            .Take(5)
            .ToListAsync();

        var newestAlbumMap = await LoadAlbumsAsync(topAlbumStats.Select(x => x.AlbumId));
        var newestAlbums = new List<NewestAlbumDto>();
        foreach (var stat in topAlbumStats)
        {
            if (newestAlbumMap.TryGetValue(stat.AlbumId, out var a))
                newestAlbums.Add(new NewestAlbumDto(a.AlbumId, a.Title, a.CoverPath, a.Artists, stat.TrackCount, stat.NewestTrackAddedAt));
        }

        return new FeaturedTracksResponse(featuredDtos, newestAlbums);
    }

    public async Task<(string? filePath, bool trackExists)> GetTrackStreamInfoAsync(int id)
    {
        var track = await _trackRepo.GetByIdAsync(id);
        if (track == null) return (null, false);
        var filePath = Path.Combine(MusicDir, track.FilePath);
        if (!File.Exists(filePath)) return (null, true);
        return (filePath, true);
    }

    public async Task<(string? filePath, string fileName, bool trackExists)> GetTrackDownloadInfoAsync(int id)
    {
        var track = await _trackRepo.GetByIdAsync(id);
        if (track == null) return (null, string.Empty, false);
        var filePath = Path.Combine(MusicDir, track.FilePath);
        if (!File.Exists(filePath)) return (null, string.Empty, true);

        // Friendly download name: "<NN> <Title>.<ext>", sanitized of path-hostile chars.
        var ext = Path.GetExtension(track.FilePath);
        var safeTitle = string.Join("_", track.Title.Split(Path.GetInvalidFileNameChars()));
        var fileName = $"{track.TrackNumber:D2} {safeTitle}{ext}";
        return (filePath, fileName, true);
    }

    public async Task<bool> IncrementPlayCountAsync(int id)
    {
        // Write-behind: buffer the play in Redis; flush to Postgres once PlayFlushEvery accrue.
        // If Redis is unavailable, IncrementAsync returns null and we fall back to a direct DB write.
        var buffered = await _cache.IncrementAsync($"plays:{id}", 1, TimeSpan.FromDays(1));
        if (buffered == null)
        {
            var rows = await _db.MusicTracks
                .Where(t => t.TrackId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.PlayCount, t => t.PlayCount + 1));
            return rows > 0;
        }

        if (buffered % PlayFlushEvery == 0)
        {
            var rows = await _db.MusicTracks
                .Where(t => t.TrackId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.PlayCount, t => t.PlayCount + PlayFlushEvery));
            if (rows == 0)
            {
                // Track no longer exists — drop the buffered counter so it can't leak.
                await _cache.RemoveAsync($"plays:{id}");
                return false;
            }
        }
        return true;
    }

    public async Task<(bool success, string? error, ScanTrackResponse? data)> ScanTrackAsync(IFormFile file)
    {
        if (file == null || file.Length == 0) return (false, "Audio file is required", null);
        if (file.Length > 100 * 1024 * 1024) return (false, "File must be under 100MB", null);
        if (!MusicHelpers.IsAllowedAudioExtension(file.FileName))
            return (false, "File must be an audio file (.mp3, .flac, .ogg, .m4a, .wav)", null);
        await using (var sniff = file.OpenReadStream())
            if (!await MusicHelpers.LooksLikeAudioAsync(sniff))
                return (false, "File content is not a recognized audio format", null);

        var tempDir = Path.Combine(MusicDir, "temp");
        Directory.CreateDirectory(tempDir);

        var guid = Guid.NewGuid().ToString();
        var ext = Path.GetExtension(file.FileName);
        var tempFileKey = $"temp_{guid}{ext}";
        var tempAudioPath = Path.Combine(tempDir, tempFileKey);
        await using (var stream = new FileStream(tempAudioPath, FileMode.Create))
            await file.CopyToAsync(stream);

        string title = Path.GetFileNameWithoutExtension(file.FileName);
        string? albumTitle = null;
        List<string> artists = new();
        string? genre = null;
        int trackNumber = 1;
        int duration = 0;
        string? tempCoverPath = null;

        try
        {
            using var tfile = TagLib.File.Create(tempAudioPath);
            if (!string.IsNullOrEmpty(tfile.Tag.Title)) title = tfile.Tag.Title;
            albumTitle = tfile.Tag.Album;
            if (tfile.Tag.Performers is { Length: > 0 }) artists = MusicHelpers.ParseArtists(tfile.Tag.Performers);
            if (!string.IsNullOrEmpty(tfile.Tag.FirstGenre)) genre = tfile.Tag.FirstGenre;
            if (tfile.Tag.Track > 0) trackNumber = (int)tfile.Tag.Track;
            duration = (int)tfile.Properties.Duration.TotalSeconds;

            if (tfile.Tag.Pictures is { Length: > 0 })
            {
                var pic = tfile.Tag.Pictures[0];
                var coverExt = pic.MimeType == "image/png" ? ".png" : ".jpg";
                var coverFileName = $"temp-cover-{guid}{coverExt}";
                await File.WriteAllBytesAsync(Path.Combine(tempDir, coverFileName), pic.Data.Data);
                tempCoverPath = $"{_publicPrefix}/music/temp/{coverFileName}";
            }
        }
        catch (Exception ex)
        {
            // Corrupt/unreadable tags shouldn't fail the scan — fall back to filename-derived metadata.
            _logger.LogWarning(ex, "Metadata extraction failed for scanned upload {File}", file.FileName);
        }

        return (true, null, new ScanTrackResponse(tempFileKey, title, albumTitle, artists, genre, trackNumber, duration, tempCoverPath));
    }

    public async Task<(bool success, string? error, MusicTrackDto? data)> UploadTrackAsync(UploadTrackRequest req)
    {
        var tempDir = Path.Combine(MusicDir, "temp");
        string? tempAudioPath;
        long fileSize;

        if (!string.IsNullOrEmpty(req.TempFileKey))
        {
            tempAudioPath = MusicHelpers.ResolveContainedPath(tempDir, req.TempFileKey);
            if (tempAudioPath == null) return (false, "Invalid temporary file reference", null);
            if (!File.Exists(tempAudioPath)) return (false, "Temporary audio file not found or expired", null);
            fileSize = new FileInfo(tempAudioPath).Length;
        }
        else
        {
            if (req.File == null || req.File.Length == 0) return (false, "Audio file or TempFileKey is required", null);
            if (req.File.Length > 100 * 1024 * 1024) return (false, "File must be under 100MB", null);
            if (!MusicHelpers.IsAllowedAudioExtension(req.File.FileName))
                return (false, "File must be an audio file (.mp3, .flac, .ogg, .m4a, .wav)", null);
            await using (var sniff = req.File.OpenReadStream())
                if (!await MusicHelpers.LooksLikeAudioAsync(sniff))
                    return (false, "File content is not a recognized audio format", null);

            Directory.CreateDirectory(tempDir);
            var ext = Path.GetExtension(req.File.FileName);
            tempAudioPath = Path.Combine(tempDir, $"temp_{Guid.NewGuid()}{ext}");
            await using (var stream = new FileStream(tempAudioPath, FileMode.Create))
                await req.File.CopyToAsync(stream);
            fileSize = req.File.Length;
        }

        string title = req.Title ?? Path.GetFileNameWithoutExtension(tempAudioPath);
        string? albumTitle = null;
        var artists = MusicHelpers.ParseArtists(req.Artists);
        string? genre = req.Genre;
        int trackNumber = req.TrackNumber ?? 1;
        int duration = req.Duration ?? 0;
        byte[]? coverData = null;
        string coverExt = ".jpg";

        try
        {
            using var tfile = TagLib.File.Create(tempAudioPath);
            if (string.IsNullOrEmpty(req.Title) && !string.IsNullOrEmpty(tfile.Tag.Title)) title = tfile.Tag.Title;
            albumTitle = tfile.Tag.Album;
            if (artists.Count == 0 && tfile.Tag.Performers is { Length: > 0 }) artists = MusicHelpers.ParseArtists(tfile.Tag.Performers);
            if (string.IsNullOrEmpty(genre) && !string.IsNullOrEmpty(tfile.Tag.FirstGenre)) genre = tfile.Tag.FirstGenre;
            if (!req.TrackNumber.HasValue && tfile.Tag.Track > 0) trackNumber = (int)tfile.Tag.Track;
            if ((!req.Duration.HasValue || duration == 0)) duration = (int)tfile.Properties.Duration.TotalSeconds;

            if (req.CoverFile == null && string.IsNullOrEmpty(req.TempCoverPath) && tfile.Tag.Pictures is { Length: > 0 })
            {
                var pic = tfile.Tag.Pictures[0];
                coverData = pic.Data.Data;
                coverExt = pic.MimeType == "image/png" ? ".png" : ".jpg";
            }
        }
        catch (Exception ex)
        {
            // Corrupt/unreadable tags shouldn't fail the upload — proceed with the request-supplied values.
            _logger.LogWarning(ex, "Metadata extraction failed during track upload");
        }

        MusicAlbum? album = null;
        if (req.AlbumId.HasValue)
            album = await _albumRepo.GetByIdAsync(req.AlbumId.Value);
        else if (!string.IsNullOrEmpty(albumTitle))
        {
            var norm = albumTitle.Trim().ToLower();
            var (albums, _) = await _albumRepo.GetPagedAsync(1, 1, a => a.Title.ToLower() == norm);
            album = albums.FirstOrDefault();
            if (album == null)
            {
                album = new MusicAlbum
                {
                    Title = albumTitle,
                    Slug = SlugHelper.GenerateSlug(albumTitle),
                    Artists = artists.Count > 0 ? new List<string>(artists) : new List<string> { "Attrition OST" },
                    Genre = genre,
                    AlbumType = "soundtrack"
                };
                await _albumRepo.AddAsync(album);
            }
        }

        if (album == null)
        {
            if (string.IsNullOrEmpty(req.TempFileKey) && File.Exists(tempAudioPath)) File.Delete(tempAudioPath);
            return (false, "Album ID is required if track has no album tag", null);
        }

        // Duplicate detection: same title + same artists + duration within 10s.
        var normTitle = title.Trim().ToLower();
        var candidates = await _trackRepo.ListAsync(
            t => t.AlbumId == album.AlbumId && t.Title.Trim().ToLower() == normTitle);
        foreach (var t in candidates)
        {
            var tArtists = t.Artists ?? new List<string>();
            bool artistsMatch = tArtists.Count == artists.Count
                && tArtists.Select(a => a.Trim().ToLower()).OrderBy(a => a)
                    .SequenceEqual(artists.Select(a => a.Trim().ToLower()).OrderBy(a => a));
            if (artistsMatch && Math.Abs(t.Duration - duration) <= 10)
            {
                if (string.IsNullOrEmpty(req.TempFileKey) && File.Exists(tempAudioPath)) File.Delete(tempAudioPath);
                return (false, $"This track already exists in the album: '{t.Title}'", null);
            }
        }

        // Resolve cover.
        var coverDir = Path.Combine(MusicDir, "covers");
        Directory.CreateDirectory(coverDir);
        string? coverPath = null;

        if (req.CoverFile is { Length: > 0 })
        {
            if (!MusicHelpers.IsAllowedImageExtension(req.CoverFile.FileName))
            {
                if (string.IsNullOrEmpty(req.TempFileKey) && File.Exists(tempAudioPath)) File.Delete(tempAudioPath);
                return (false, "Cover must be an image (.jpg, .png, .webp)", null);
            }
            await using (var check = req.CoverFile.OpenReadStream())
            {
                if (!await MusicHelpers.LooksLikeImageAsync(check))
                {
                    if (string.IsNullOrEmpty(req.TempFileKey) && File.Exists(tempAudioPath)) File.Delete(tempAudioPath);
                    return (false, "Cover file is not a valid image", null);
                }
            }
            var ext = Path.GetExtension(req.CoverFile.FileName);
            var coverFileName = $"track-{Guid.NewGuid()}{ext}";
            await using var stream = new FileStream(Path.Combine(coverDir, coverFileName), FileMode.Create);
            await req.CoverFile.CopyToAsync(stream);
            coverPath = $"{_publicPrefix}/music/covers/{coverFileName}";
        }
        else if (!string.IsNullOrEmpty(req.TempCoverPath))
        {
            var src = MusicHelpers.ResolveContainedPath(tempDir, req.TempCoverPath);
            if (src != null && MusicHelpers.IsAllowedImageExtension(src) && File.Exists(src))
            {
                // Validate magic bytes too (the direct-upload path does); a .png-named non-image
                // shouldn't be promoted into the public covers dir just because it sits in temp.
                bool looksValid;
                await using (var check = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read))
                    looksValid = await MusicHelpers.LooksLikeImageAsync(check);

                if (looksValid)
                {
                    var ext = Path.GetExtension(src);
                    var coverFileName = $"track-{Guid.NewGuid()}{ext}";
                    if (await SafeFileOperations.SafeMoveAsync(src, Path.Combine(coverDir, coverFileName), _logger))
                        coverPath = $"{_publicPrefix}/music/covers/{coverFileName}";
                }
                else
                {
                    await SafeFileOperations.SafeDeleteAsync(src, _logger);
                }
            }
        }
        else if (coverData != null)
        {
            var coverFileName = $"track-{Guid.NewGuid()}{coverExt}";
            await File.WriteAllBytesAsync(Path.Combine(coverDir, coverFileName), coverData);
            coverPath = $"{_publicPrefix}/music/covers/{coverFileName}";
        }

        // Move audio into place.
        var trackDir = Path.Combine(MusicDir, "tracks");
        Directory.CreateDirectory(trackDir);
        var slug = await SlugHelper.GenerateUniqueSlugAsync(title, async s =>
            await _trackRepo.CountAsync(t => t.Slug == s) > 0);
        var audioExt = Path.GetExtension(tempAudioPath);
        var fileName = $"{trackNumber:D2}-{Guid.NewGuid().ToString()[..8]}-{slug}{audioExt}";
        if (!await SafeFileOperations.SafeMoveAsync(tempAudioPath, Path.Combine(trackDir, fileName), _logger))
            return (false, "Could not move the uploaded file into storage. Please try again.", null);

        var track = new MusicTrack
        {
            AlbumId = album.AlbumId,
            Title = title,
            Slug = slug,
            Artists = artists.Count > 0 ? artists : new List<string> { "Attrition OST" },
            TrackNumber = trackNumber,
            Duration = duration,
            FilePath = $"tracks/{fileName}",
            FileSize = fileSize,
            Genre = genre,
            CoverPath = coverPath,
            IsFeatured = req.IsFeatured
        };
        await _trackRepo.AddAsync(track);
        await RescanAlbumAggregatesAsync(album.AlbumId);

        return (true, null, new MusicTrackDto(track.TrackId, track.AlbumId, track.Title, track.Slug,
            track.TrackNumber, track.Artists, track.Duration, track.Genre, track.CoverPath, track.PlayCount,
            track.IsFeatured, track.FileSize ?? 0, album.Title, album.CoverPath));
    }

    public async Task<(bool success, string? error, MusicTrackDto? data)> UpdateTrackAsync(int id, UpdateTrackRequest req)
    {
        var track = await _trackRepo.GetByIdAsync(id);
        if (track == null) return (false, "Track not found", null);

        track.Title = req.Title ?? track.Title;
        if (req.Artists != null)
        {
            var parsed = MusicHelpers.ParseArtists(req.Artists);
            if (parsed.Count > 0) track.Artists = parsed;
        }
        track.TrackNumber = req.TrackNumber ?? track.TrackNumber;
        track.Duration = req.Duration ?? track.Duration;
        track.Genre = req.Genre ?? track.Genre;
        track.IsFeatured = req.IsFeatured ?? track.IsFeatured;

        await _trackRepo.UpdateAsync(track);
        await RescanAlbumAggregatesAsync(track.AlbumId);

        var album = await _albumRepo.GetByIdAsync(track.AlbumId);
        return (true, null, new MusicTrackDto(track.TrackId, track.AlbumId, track.Title, track.Slug,
            track.TrackNumber, track.Artists, track.Duration, track.Genre, track.CoverPath, track.PlayCount,
            track.IsFeatured, track.FileSize ?? 0, album?.Title, album?.CoverPath));
    }

    public async Task<bool> DeleteTrackAsync(int id)
    {
        var track = await _trackRepo.GetByIdAsync(id);
        if (track == null) return false;

        var filePath = Path.Combine(MusicDir, track.FilePath);
        // Remove the file before the DB row; if the file is locked, keep the row so we don't
        // end up with a DB record pointing at an orphaned-but-undeletable file.
        if (!await SafeFileOperations.SafeDeleteAsync(filePath, _logger))
            return false;

        var albumId = track.AlbumId;
        await _trackRepo.DeleteAsync(track);
        await RescanAlbumAggregatesAsync(albumId);
        return true;
    }

    public Task<int> CountAsync() => _trackRepo.CountAsync();
}
