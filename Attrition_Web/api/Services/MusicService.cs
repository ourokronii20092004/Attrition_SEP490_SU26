using Attrition.API.Data;
using Attrition.API.DTOs;
using Attrition.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Attrition.API.Services;

public class MusicService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public MusicService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public string GetUploadPath() => _config["FileUpload:UploadPath"] ?? "./uploads";

    private List<string> ParseArtists(IEnumerable<string>? input)
    {
        if (input == null) return new List<string>();
        var result = new List<string>();
        var separators = new char[] { ',', '/', ';', '|', '\\' };
        foreach (var entry in input)
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            var split = entry.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            foreach (var s in split)
            {
                var trimmed = s.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !result.Contains(trimmed))
                {
                    result.Add(trimmed);
                }
            }
        }
        return result;
    }

    // ─── Albums (Public) ───

    public async Task<object> GetAlbumsAsync()
    {
        var albums = await _db.MusicAlbums
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.AlbumId, a.Title, a.Slug, a.Artists, a.Description,
                a.CoverPath, a.IsCoverUserDefined, a.ReleaseDate, a.AlbumType, a.Genre,
                a.TrackCount, a.TotalDuration, a.CreatedAt
            })
            .ToListAsync();
        return new { success = true, data = albums };
    }

    public async Task<object?> GetAlbumAsync(int id)
    {
        var album = await _db.MusicAlbums
            .Include(a => a.Tracks.OrderBy(t => t.TrackNumber))
            .FirstOrDefaultAsync(a => a.AlbumId == id);
        if (album == null) return null;

        return new { success = true, data = new {
            album.AlbumId, album.Title, album.Slug, album.Artists, album.Description,
            album.CoverPath, album.IsCoverUserDefined, album.ReleaseDate, album.AlbumType, album.Genre,
            album.TrackCount, album.TotalDuration, album.CreatedAt,
            Tracks = album.Tracks.Select(t => new {
                t.TrackId, t.AlbumId, t.Title, t.Slug, t.TrackNumber, t.Artists,
                t.Duration, t.Genre, t.CoverPath, t.PlayCount, t.IsFeatured, t.FileSize
            })
        } };
    }

    public async Task<object> GetTracksAsync(int? albumId)
    {
        var query = _db.MusicTracks.AsQueryable();
        if (albumId.HasValue) query = query.Where(t => t.AlbumId == albumId.Value);

        var tracks = await query
            .OrderBy(t => t.AlbumId).ThenBy(t => t.TrackNumber)
            .Select(t => new
            {
                t.TrackId, t.AlbumId, t.Title, t.Slug, t.TrackNumber, t.Artists,
                t.Duration, t.Genre, t.CoverPath, t.PlayCount, t.IsFeatured, t.FileSize,
                AlbumTitle = t.Album.Title, AlbumCoverPath = t.Album.CoverPath
            })
            .ToListAsync();
        return new { success = true, data = tracks };
    }

    public async Task<object> GetFeaturedTracksAsync()
    {
        var lastMonthLimit = DateTime.UtcNow.AddDays(-30);

        var featuredTracks = await _db.MusicTracks
            .Where(t => t.CreatedAt >= lastMonthLimit)
            .OrderByDescending(t => t.PlayCount)
            .Take(10)
            .Select(t => new
            {
                t.TrackId, t.AlbumId, t.Title, t.Slug, t.TrackNumber, t.Artists,
                t.Duration, t.Genre, t.CoverPath, t.PlayCount, t.FileSize,
                AlbumTitle = t.Album.Title, AlbumCoverPath = t.Album.CoverPath,
                t.CreatedAt
            })
            .ToListAsync();

        if (featuredTracks.Count < 10)
        {
            var excludedIds = featuredTracks.Select(t => t.TrackId).ToList();
            var fallbackTracks = await _db.MusicTracks
                .Where(t => !excludedIds.Contains(t.TrackId))
                .OrderByDescending(t => t.PlayCount)
                .Take(10 - featuredTracks.Count)
                .Select(t => new
                {
                    t.TrackId, t.AlbumId, t.Title, t.Slug, t.TrackNumber, t.Artists,
                    t.Duration, t.Genre, t.CoverPath, t.PlayCount, t.FileSize,
                    AlbumTitle = t.Album.Title, AlbumCoverPath = t.Album.CoverPath,
                    t.CreatedAt
                })
                .ToListAsync();

            featuredTracks.AddRange(fallbackTracks);
        }

        featuredTracks = featuredTracks.OrderByDescending(t => t.PlayCount).ToList();

        var newestAlbums = await _db.MusicAlbums
            .Where(a => a.Tracks.Any())
            .OrderByDescending(a => a.Tracks.Max(t => t.CreatedAt))
            .Take(5)
            .Select(a => new
            {
                a.AlbumId,
                a.Title,
                a.CoverPath,
                Artists = a.Artists,
                TrackCount = a.Tracks.Count,
                NewestTrackAddedAt = a.Tracks.Max(t => t.CreatedAt)
            })
            .ToListAsync();

        return new { success = true, data = new { featuredTracks, newestAlbums } };
    }

    // ─── Streaming ───

    public async Task<(string? filePath, bool trackExists)> GetTrackStreamInfoAsync(int id)
    {
        var track = await _db.MusicTracks.FindAsync(id);
        if (track == null) return (null, false);

        var filePath = Path.Combine(GetUploadPath(), "music", track.FilePath);
        if (!System.IO.File.Exists(filePath)) return (null, true);

        return (filePath, true);
    }

    public async Task<bool> IncrementPlayCountAsync(int id)
    {
        var track = await _db.MusicTracks.FindAsync(id);
        if (track == null) return false;

        track.PlayCount++;
        await _db.SaveChangesAsync();
        return true;
    }

    // ─── Favorites ───

    public async Task<object> GetFavoritesAsync(Guid userId)
    {
        var favorites = await _db.UserFavorites
            .Where(f => f.UserId == userId)
            .Include(f => f.Track)
                .ThenInclude(t => t.Album)
            .OrderByDescending(f => f.AddedAt)
            .Select(f => new
            {
                f.Track.TrackId, f.Track.AlbumId, f.Track.Title, f.Track.Slug, f.Track.Artists,
                f.Track.TrackNumber, f.Track.Duration, f.Track.Genre, f.Track.CoverPath, f.Track.PlayCount,
                AlbumTitle = f.Track.Album.Title, AlbumCoverPath = f.Track.Album.CoverPath,
                FavoritedAt = f.AddedAt
            })
            .ToListAsync();
        return new { success = true, data = favorites };
    }

    public async Task<object> GetFavoriteIdsAsync(Guid userId)
    {
        var ids = await _db.UserFavorites
            .Where(f => f.UserId == userId)
            .Select(f => f.TrackId)
            .ToListAsync();
        return new { success = true, data = ids };
    }

    public async Task<(bool success, bool isFavorited, string? error)> ToggleFavoriteAsync(Guid userId, int trackId)
    {
        var existing = await _db.UserFavorites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.TrackId == trackId);

        if (existing != null)
        {
            _db.UserFavorites.Remove(existing);
            await _db.SaveChangesAsync();
            return (true, false, null);
        }

        var track = await _db.MusicTracks.FindAsync(trackId);
        if (track == null) return (false, false, "Track not found");

        _db.UserFavorites.Add(new UserFavorite { UserId = userId, TrackId = trackId });
        await _db.SaveChangesAsync();
        return (true, true, null);
    }

    // ─── Admin: Album CRUD ───

    public async Task<object> CreateAlbumAsync(CreateAlbumRequest req)
    {
        var parsedArtists = ParseArtists(req.Artists);
        var album = new MusicAlbum
        {
            Title = req.Title,
            Slug = req.Title.ToLower().Replace(" ", "-").Replace("'", ""),
            Artists = parsedArtists.Count > 0 ? parsedArtists : new List<string> { "Attrition OST" },
            Description = req.Description,
            Genre = req.Genre,
            AlbumType = req.AlbumType ?? "soundtrack",
            ReleaseDate = req.ReleaseDate,
            SortOrder = req.SortOrder
        };

        _db.MusicAlbums.Add(album);
        await _db.SaveChangesAsync();
        return new { success = true, data = album };
    }

    public async Task<object?> UpdateAlbumAsync(int id, CreateAlbumRequest req)
    {
        var album = await _db.MusicAlbums.FindAsync(id);
        if (album == null) return null;

        album.Title = req.Title;
        album.Slug = req.Title.ToLower().Replace(" ", "-").Replace("'", "");
        if (req.Artists != null)
        {
            var parsed = ParseArtists(req.Artists);
            if (parsed.Count > 0) album.Artists = parsed;
        }
        album.Description = req.Description;
        album.Genre = req.Genre ?? album.Genre;
        album.AlbumType = req.AlbumType ?? album.AlbumType;
        album.ReleaseDate = req.ReleaseDate;
        album.SortOrder = req.SortOrder;

        await _db.SaveChangesAsync();
        return new { success = true, data = album };
    }

    public async Task<bool> DeleteAlbumAsync(int id)
    {
        var album = await _db.MusicAlbums.Include(a => a.Tracks).FirstOrDefaultAsync(a => a.AlbumId == id);
        if (album == null) return false;

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
        return true;
    }

    public async Task<(bool success, string? error, string? coverPath)> UploadAlbumCoverAsync(int id, IFormFile file)
    {
        var album = await _db.MusicAlbums.FindAsync(id);
        if (album == null) return (false, "Album not found", null);

        if (file.Length > 10 * 1024 * 1024) // 10MB limit for covers
            return (false, "Cover image must be under 10MB", null);

        var coverDir = Path.Combine(GetUploadPath(), "music", "covers");
        Directory.CreateDirectory(coverDir);

        var ext = Path.GetExtension(file.FileName);
        var fileName = $"album-{id}{ext}";
        var filePath = Path.Combine(coverDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        album.CoverPath = $"/uploads/music/covers/{fileName}";
        album.IsCoverUserDefined = true; // Mark as user defined
        await _db.SaveChangesAsync();

        return (true, null, album.CoverPath);
    }

    // ─── Admin: Track CRUD ───

    private async Task RescanAlbumAggregatesAsync(int albumId)
    {
        var album = await _db.MusicAlbums
            .Include(a => a.Tracks)
            .FirstOrDefaultAsync(a => a.AlbumId == albumId);

        if (album == null) return;

        var tracks = album.Tracks.OrderBy(t => t.TrackNumber).ToList();

        // 1. Recalculate basic aggregates
        album.TrackCount = tracks.Count;
        album.TotalDuration = tracks.Sum(t => t.Duration);

        // 2. Aggregate unique genres
        album.Genre = tracks.Select(t => t.Genre)
            .Where(g => !string.IsNullOrEmpty(g))
            .Distinct()
            .FirstOrDefault(); // Pull first genre

        // 3. Aggregate unique artists
        album.Artists = tracks.SelectMany(t => t.Artists)
            .Where(artistName => !string.IsNullOrEmpty(artistName))
            .Distinct()
            .ToList();

        if (album.Artists.Count == 0)
        {
            album.Artists = new List<string> { "Attrition OST" };
        }

        // 4. Resolve album cover (with Override Protection)
        if (!album.IsCoverUserDefined)
        {
            // Try to pick the first cover image found among tracks
            var firstTrackWithCover = tracks.FirstOrDefault(t => !string.IsNullOrEmpty(t.CoverPath));
            if (firstTrackWithCover != null)
            {
                album.CoverPath = firstTrackWithCover.CoverPath;
            }
            else
            {
                album.CoverPath = null; // Clear if no tracks have covers anymore
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task<(bool success, string? error, object? data)> ScanTrackAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return (false, "Audio file is required", null);

        if (file.Length > 100 * 1024 * 1024) // 100MB limit
            return (false, "File must be under 100MB", null);

        var tempDir = Path.Combine(GetUploadPath(), "music", "temp");
        Directory.CreateDirectory(tempDir);

        // Simple non-blocking cleanup of temp files older than 24 hours
        try
        {
            var files = Directory.GetFiles(tempDir);
            foreach (var f in files)
            {
                var fi = new FileInfo(f);
                if (fi.CreationTimeUtc < DateTime.UtcNow.AddDays(-1))
                {
                    System.IO.File.Delete(f);
                }
            }
        }
        catch { /* ignore cleanup errors */ }

        var guid = Guid.NewGuid().ToString();
        var ext = Path.GetExtension(file.FileName);
        var tempFileKey = $"temp_{guid}{ext}";
        var tempAudioPath = Path.Combine(tempDir, tempFileKey);

        using (var stream = new FileStream(tempAudioPath, FileMode.Create))
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
            using (var tfile = TagLib.File.Create(tempAudioPath))
            {
                if (!string.IsNullOrEmpty(tfile.Tag.Title)) title = tfile.Tag.Title;
                albumTitle = tfile.Tag.Album;
                if (tfile.Tag.Performers != null && tfile.Tag.Performers.Length > 0)
                {
                    artists = ParseArtists(tfile.Tag.Performers);
                }
                if (!string.IsNullOrEmpty(tfile.Tag.FirstGenre)) genre = tfile.Tag.FirstGenre;
                if (tfile.Tag.Track > 0) trackNumber = (int)tfile.Tag.Track;
                duration = (int)tfile.Properties.Duration.TotalSeconds;

                if (tfile.Tag.Pictures != null && tfile.Tag.Pictures.Length > 0)
                {
                    var pic = tfile.Tag.Pictures[0];
                    var coverExt = pic.MimeType == "image/png" ? ".png" : ".jpg";
                    var coverFileName = $"temp-cover-{guid}{coverExt}";
                    var tempCoverFullPath = Path.Combine(tempDir, coverFileName);
                    await System.IO.File.WriteAllBytesAsync(tempCoverFullPath, pic.Data.Data);
                    tempCoverPath = $"/uploads/music/temp/{coverFileName}";
                }
            }
        }
        catch
        {
            // Ignore parsing errors, return what we can
        }

        if (!string.IsNullOrEmpty(albumTitle))
        {
            var normAlbumTitle = albumTitle.Trim().ToLower();
            var album = await _db.MusicAlbums.FirstOrDefaultAsync(a => a.Title.ToLower() == normAlbumTitle);
            if (album != null)
            {
                var normTitle = title.Trim().ToLower();
                var candidateTracks = await _db.MusicTracks
                    .Where(t => t.AlbumId == album.AlbumId && t.Title.Trim().ToLower() == normTitle)
                    .ToListAsync();

                foreach (var t in candidateTracks)
                {
                    bool artistsMatch = false;
                    var tArtists = t.Artists ?? new List<string>();
                    if (tArtists.Count == artists.Count)
                    {
                        var sortedT = tArtists.Select(a => a.Trim().ToLower()).OrderBy(a => a);
                        var sortedNew = artists.Select(a => a.Trim().ToLower()).OrderBy(a => a);
                        if (sortedT.SequenceEqual(sortedNew))
                        {
                            artistsMatch = true;
                        }
                    }

                    if (artistsMatch && Math.Abs(t.Duration - duration) <= 10)
                    {
                        if (System.IO.File.Exists(tempAudioPath)) System.IO.File.Delete(tempAudioPath);
                        return (false, $"This track already exists in the album: '{t.Title}'", null);
                    }
                }
            }
        }

        return (true, null, new
        {
            tempFileKey,
            title,
            album = albumTitle,
            artists,
            genre,
            trackNumber,
            duration,
            tempCoverPath
        });
    }

    public async Task<(bool success, string? error, object? data)> UploadTrackAsync(UploadTrackRequest req)
    {
        string? tempAudioPath = null;
        string? tempFileKey = req.TempFileKey;
        long fileSize = 0;

        if (!string.IsNullOrEmpty(tempFileKey))
        {
            var tempDir = Path.Combine(GetUploadPath(), "music", "temp");
            tempAudioPath = Path.Combine(tempDir, tempFileKey);
            if (!System.IO.File.Exists(tempAudioPath))
            {
                return (false, "Temporary audio file not found or expired", null);
            }
            fileSize = new FileInfo(tempAudioPath).Length;
        }
        else
        {
            if (req.File == null || req.File.Length == 0)
                return (false, "Audio file or TempFileKey is required", null);

            if (req.File.Length > 100 * 1024 * 1024) // 100MB limit
                return (false, "File must be under 100MB", null);

            var tempDir = Path.Combine(GetUploadPath(), "music", "temp");
            Directory.CreateDirectory(tempDir);
            var guid = Guid.NewGuid().ToString();
            var ext = Path.GetExtension(req.File.FileName);
            tempFileKey = $"temp_{guid}{ext}";
            tempAudioPath = Path.Combine(tempDir, tempFileKey);

            using (var stream = new FileStream(tempAudioPath, FileMode.Create))
                await req.File.CopyToAsync(stream);

            fileSize = req.File.Length;
        }

        string title = req.Title ?? Path.GetFileNameWithoutExtension(tempAudioPath);
        string? albumTitle = null;
        List<string> artists = ParseArtists(req.Artists);
        string? genre = req.Genre;
        int trackNumber = req.TrackNumber ?? 1;
        int duration = req.Duration ?? 0;
        byte[]? coverData = null;
        string coverExt = ".jpg";

        // If we don't have some values, try to scan them using TagLib
        try
        {
            using (var tfile = TagLib.File.Create(tempAudioPath))
            {
                if (string.IsNullOrEmpty(req.Title) && !string.IsNullOrEmpty(tfile.Tag.Title)) title = tfile.Tag.Title;
                albumTitle = tfile.Tag.Album;
                if (artists.Count == 0 && tfile.Tag.Performers != null && tfile.Tag.Performers.Length > 0)
                {
                    artists = ParseArtists(tfile.Tag.Performers);
                }
                if (string.IsNullOrEmpty(genre) && !string.IsNullOrEmpty(tfile.Tag.FirstGenre)) genre = tfile.Tag.FirstGenre;
                if (!req.TrackNumber.HasValue && tfile.Tag.Track > 0) trackNumber = (int)tfile.Tag.Track;
                if (!req.Duration.HasValue || duration == 0) duration = (int)tfile.Properties.Duration.TotalSeconds;

                // Only scan cover from audio file if user didn't provide a cover file or temp cover path
                if (req.CoverFile == null && string.IsNullOrEmpty(req.TempCoverPath))
                {
                    if (tfile.Tag.Pictures != null && tfile.Tag.Pictures.Length > 0)
                    {
                        var pic = tfile.Tag.Pictures[0];
                        coverData = pic.Data.Data;
                        coverExt = pic.MimeType == "image/png" ? ".png" : ".jpg";
                    }
                }
            }
        }
        catch { /* ignore parsing errors */ }

        MusicAlbum? album = null;
        if (req.AlbumId.HasValue)
        {
            album = await _db.MusicAlbums.FindAsync(req.AlbumId.Value);
        }
        else if (!string.IsNullOrEmpty(albumTitle))
        {
            var normAlbumTitle = albumTitle.Trim().ToLower();
            album = await _db.MusicAlbums.FirstOrDefaultAsync(a => a.Title.ToLower() == normAlbumTitle);
            if (album == null)
            {
                album = new MusicAlbum
                {
                    Title = albumTitle,
                    Slug = albumTitle.ToLower().Replace(" ", "-").Replace("'", ""),
                    Artists = artists.Count > 0 ? new List<string>(artists) : new List<string> { "Attrition OST" },
                    Genre = genre,
                    AlbumType = "soundtrack"
                };
                _db.MusicAlbums.Add(album);
                await _db.SaveChangesAsync();
            }
        }

        if (album == null)
        {
            if (string.IsNullOrEmpty(req.TempFileKey) && System.IO.File.Exists(tempAudioPath))
                System.IO.File.Delete(tempAudioPath);

            return (false, "Album ID is required if track has no album tag", null);
        }

        // Check for duplicate track in this album
        var normTitle = title.Trim().ToLower();
        var candidateTracks = await _db.MusicTracks
            .Where(t => t.AlbumId == album.AlbumId && t.Title.Trim().ToLower() == normTitle)
            .ToListAsync();

        foreach (var t in candidateTracks)
        {
            bool artistsMatch = false;
            var tArtists = t.Artists ?? new List<string>();
            if (tArtists.Count == artists.Count)
            {
                var sortedT = tArtists.Select(a => a.Trim().ToLower()).OrderBy(a => a);
                var sortedNew = artists.Select(a => a.Trim().ToLower()).OrderBy(a => a);
                if (sortedT.SequenceEqual(sortedNew))
                {
                    artistsMatch = true;
                }
            }

            if (artistsMatch && Math.Abs(t.Duration - duration) <= 10)
            {
                if (string.IsNullOrEmpty(req.TempFileKey) && System.IO.File.Exists(tempAudioPath))
                    System.IO.File.Delete(tempAudioPath);

                return (false, $"This track already exists in the album: '{t.Title}'", null);
            }
        }

        string? coverPath = null;
        var coverDir = Path.Combine(GetUploadPath(), "music", "covers");
        Directory.CreateDirectory(coverDir);

        if (req.CoverFile != null && req.CoverFile.Length > 0)
        {
            var extCover = Path.GetExtension(req.CoverFile.FileName);
            var coverFileName = $"track-{Guid.NewGuid()}{extCover}";
            var fullCoverPath = Path.Combine(coverDir, coverFileName);

            using (var stream = new FileStream(fullCoverPath, FileMode.Create))
                await req.CoverFile.CopyToAsync(stream);

            coverPath = $"/uploads/music/covers/{coverFileName}";
        }
        else if (!string.IsNullOrEmpty(req.TempCoverPath))
        {
            var tempCoverFileName = Path.GetFileName(req.TempCoverPath);
            var srcCoverPath = Path.Combine(GetUploadPath(), "music", "temp", tempCoverFileName);
            if (System.IO.File.Exists(srcCoverPath))
            {
                var extCover = Path.GetExtension(tempCoverFileName);
                var coverFileName = $"track-{Guid.NewGuid()}{extCover}";
                var fullCoverPath = Path.Combine(coverDir, coverFileName);
                System.IO.File.Move(srcCoverPath, fullCoverPath, true);
                coverPath = $"/uploads/music/covers/{coverFileName}";
            }
        }
        else if (coverData != null)
        {
            var coverFileName = $"track-{Guid.NewGuid()}{coverExt}";
            var fullCoverPath = Path.Combine(coverDir, coverFileName);
            await System.IO.File.WriteAllBytesAsync(fullCoverPath, coverData);
            coverPath = $"/uploads/music/covers/{coverFileName}";
        }

        var trackDir = Path.Combine(GetUploadPath(), "music", "tracks");
        Directory.CreateDirectory(trackDir);

        var slug = title.ToLower().Replace(" ", "-").Replace("'", "");
        var audioExt = Path.GetExtension(tempAudioPath);
        var fileName = $"{trackNumber:D2}-{Guid.NewGuid().ToString().Substring(0, 8)}-{slug}{audioExt}";
        var finalPath = Path.Combine(trackDir, fileName);

        System.IO.File.Move(tempAudioPath, finalPath, true);

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

        _db.MusicTracks.Add(track);
        await _db.SaveChangesAsync();

        // Update album stats
        await RescanAlbumAggregatesAsync(album.AlbumId);

        return (true, null, new { track.TrackId, track.AlbumId, track.Title, track.Slug, track.TrackNumber, track.Artists, track.Duration, track.FilePath, track.FileSize, track.Genre, track.CoverPath, track.IsFeatured });
    }

    public async Task<(bool success, string? error, object? data)> UpdateTrackAsync(int id, UpdateTrackRequest req)
    {
        var track = await _db.MusicTracks.FindAsync(id);
        if (track == null) return (false, "Track not found", null);

        track.Title = req.Title ?? track.Title;
        if (req.Artists != null)
        {
            var parsed = ParseArtists(req.Artists);
            if (parsed.Count > 0) track.Artists = parsed;
        }
        track.TrackNumber = req.TrackNumber ?? track.TrackNumber;
        track.Duration = req.Duration ?? track.Duration;
        track.Genre = req.Genre ?? track.Genre;
        track.IsFeatured = req.IsFeatured ?? track.IsFeatured;

        await _db.SaveChangesAsync();
        await RescanAlbumAggregatesAsync(track.AlbumId);

        return (true, null, new { track.TrackId, track.AlbumId, track.Title, track.Slug, track.Artists, track.TrackNumber, track.Duration, track.Genre, track.IsFeatured });
    }

    public async Task<bool> DeleteTrackAsync(int id)
    {
        var track = await _db.MusicTracks.FindAsync(id);
        if (track == null) return false;

        // Delete track file
        var filePath = Path.Combine(GetUploadPath(), "music", track.FilePath);
        if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);

        // Delete track-specific cover file if it exists
        if (!string.IsNullOrEmpty(track.CoverPath))
        {
            var coverFullPath = Path.Combine(GetUploadPath(), track.CoverPath.TrimStart('/'));
            if (System.IO.File.Exists(coverFullPath)) System.IO.File.Delete(coverFullPath);
        }

        var albumId = track.AlbumId;
        _db.MusicTracks.Remove(track);
        await _db.SaveChangesAsync();

        // Update album stats
        await RescanAlbumAggregatesAsync(albumId);

        return true;
    }
}
