using System.Linq.Expressions;
using BuildingBlocks.Contracts;
using BuildingBlocks.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Music.Service.Data;
using Music.Service.DTOs;
using Music.Service.Models;

namespace Music.Service.Services;

public class TrackService : ITrackService
{
    private readonly IRepository<MusicAlbum> _albumRepo;
    private readonly IRepository<MusicTrack> _trackRepo;
    private readonly MusicDbContext _db;
    private readonly string _uploadPath;
    private readonly string _publicPrefix;

    public TrackService(IRepository<MusicAlbum> albumRepo, IRepository<MusicTrack> trackRepo, MusicDbContext db, IConfiguration config)
    {
        _albumRepo = albumRepo;
        _trackRepo = trackRepo;
        _db = db;
        _uploadPath = config["FileUpload:UploadPath"] ?? "/app/uploads";
        _publicPrefix = config["FileUpload:PublicPrefix"] ?? "/api/music/media";
    }

    private string MusicDir => Path.Combine(_uploadPath, "music");

    private async Task RescanAlbumAggregatesAsync(int albumId)
    {
        var album = await _albumRepo.GetByIdAsync(albumId);
        if (album == null) return;

        var (tracks, _) = await _trackRepo.GetPagedAsync(1, int.MaxValue, t => t.AlbumId == albumId,
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
        var (tracks, _) = await _trackRepo.GetPagedAsync(1, int.MaxValue, filter,
            q => q.OrderBy(t => t.AlbumId).ThenBy(t => t.TrackNumber));

        var dtos = new List<MusicTrackDto>();
        foreach (var t in tracks)
        {
            var album = await _albumRepo.GetByIdAsync(t.AlbumId);
            dtos.Add(new MusicTrackDto(t.TrackId, t.AlbumId, t.Title, t.Slug, t.TrackNumber, t.Artists,
                t.Duration, t.Genre, t.CoverPath, t.PlayCount, t.IsFeatured, t.FileSize ?? 0, album?.Title, album?.CoverPath));
        }
        return dtos;
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
        var featuredDtos = new List<MusicTrackDto>();
        foreach (var t in sorted)
        {
            var album = await _albumRepo.GetByIdAsync(t.AlbumId);
            featuredDtos.Add(new MusicTrackDto(t.TrackId, t.AlbumId, t.Title, t.Slug, t.TrackNumber, t.Artists,
                t.Duration, t.Genre, t.CoverPath, t.PlayCount, t.IsFeatured, t.FileSize ?? 0, album?.Title, album?.CoverPath));
        }

        var albums = await _albumRepo.GetAllAsync();
        var newestAlbums = new List<NewestAlbumDto>();
        foreach (var a in albums)
        {
            var (tracks, _) = await _trackRepo.GetPagedAsync(1, int.MaxValue, t => t.AlbumId == a.AlbumId);
            if (tracks.Count > 0)
                newestAlbums.Add(new NewestAlbumDto(a.AlbumId, a.Title, a.CoverPath, a.Artists, tracks.Count, tracks.Max(t => t.CreatedAt)));
        }

        return new FeaturedTracksResponse(featuredDtos,
            newestAlbums.OrderByDescending(na => na.NewestTrackAddedAt).Take(5).ToList());
    }

    public async Task<(string? filePath, bool trackExists)> GetTrackStreamInfoAsync(int id)
    {
        var track = await _trackRepo.GetByIdAsync(id);
        if (track == null) return (null, false);
        var filePath = Path.Combine(MusicDir, track.FilePath);
        if (!File.Exists(filePath)) return (null, true);
        return (filePath, true);
    }

    public async Task<bool> IncrementPlayCountAsync(int id)
    {
        var rows = await _db.MusicTracks
            .Where(t => t.TrackId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.PlayCount, t => t.PlayCount + 1));
        return rows > 0;
    }

    public async Task<(bool success, string? error, ScanTrackResponse? data)> ScanTrackAsync(IFormFile file)
    {
        if (file == null || file.Length == 0) return (false, "Audio file is required", null);
        if (file.Length > 100 * 1024 * 1024) return (false, "File must be under 100MB", null);

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
        catch { }

        return (true, null, new ScanTrackResponse(tempFileKey, title, albumTitle, artists, genre, trackNumber, duration, tempCoverPath));
    }

    public async Task<(bool success, string? error, MusicTrackDto? data)> UploadTrackAsync(UploadTrackRequest req)
    {
        var tempDir = Path.Combine(MusicDir, "temp");
        string? tempAudioPath;
        long fileSize;

        if (!string.IsNullOrEmpty(req.TempFileKey))
        {
            tempAudioPath = Path.Combine(tempDir, req.TempFileKey);
            if (!File.Exists(tempAudioPath)) return (false, "Temporary audio file not found or expired", null);
            fileSize = new FileInfo(tempAudioPath).Length;
        }
        else
        {
            if (req.File == null || req.File.Length == 0) return (false, "Audio file or TempFileKey is required", null);
            if (req.File.Length > 100 * 1024 * 1024) return (false, "File must be under 100MB", null);

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
        catch { }

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
        var (candidates, _) = await _trackRepo.GetPagedAsync(1, int.MaxValue,
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
            var ext = Path.GetExtension(req.CoverFile.FileName);
            var coverFileName = $"track-{Guid.NewGuid()}{ext}";
            await using var stream = new FileStream(Path.Combine(coverDir, coverFileName), FileMode.Create);
            await req.CoverFile.CopyToAsync(stream);
            coverPath = $"{_publicPrefix}/music/covers/{coverFileName}";
        }
        else if (!string.IsNullOrEmpty(req.TempCoverPath))
        {
            var tempCoverFileName = Path.GetFileName(req.TempCoverPath);
            var src = Path.Combine(tempDir, tempCoverFileName);
            if (File.Exists(src))
            {
                var ext = Path.GetExtension(tempCoverFileName);
                var coverFileName = $"track-{Guid.NewGuid()}{ext}";
                File.Move(src, Path.Combine(coverDir, coverFileName), true);
                coverPath = $"{_publicPrefix}/music/covers/{coverFileName}";
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
        var slug = SlugHelper.GenerateSlug(title);
        var audioExt = Path.GetExtension(tempAudioPath);
        var fileName = $"{trackNumber:D2}-{Guid.NewGuid().ToString()[..8]}-{slug}{audioExt}";
        File.Move(tempAudioPath, Path.Combine(trackDir, fileName), true);

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
        if (File.Exists(filePath)) File.Delete(filePath);

        var albumId = track.AlbumId;
        await _trackRepo.DeleteAsync(track);
        await RescanAlbumAggregatesAsync(albumId);
        return true;
    }

    public Task<int> CountAsync() => _trackRepo.CountAsync();
}
