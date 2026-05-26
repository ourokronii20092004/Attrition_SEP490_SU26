using Attrition.API.DTOs;
using Attrition.API.Models;
using Attrition.API.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Attrition.API.Services;

public class TrackService : ITrackService
{
    private readonly IRepository<MusicAlbum> _albumRepo;
    private readonly IRepository<MusicTrack> _trackRepo;
    private readonly IConfiguration _config;

    public TrackService(
        IRepository<MusicAlbum> albumRepo,
        IRepository<MusicTrack> trackRepo,
        IConfiguration config)
    {
        _albumRepo = albumRepo;
        _trackRepo = trackRepo;
        _config = config;
    }

    private string GetUploadPath() => _config["FileUpload:UploadPath"] ?? "./uploads";

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

    private async Task RescanAlbumAggregatesAsync(int albumId)
    {
        var album = await _albumRepo.GetByIdAsync(albumId);
        if (album == null) return;

        var (tracks, _) = await _trackRepo.GetPagedAsync(
            1, int.MaxValue, t => t.AlbumId == albumId,
            q => q.OrderBy(t => t.TrackNumber)
        );

        album.TrackCount = tracks.Count;
        album.TotalDuration = tracks.Sum(t => t.Duration);

        album.Genre = tracks.Select(t => t.Genre)
            .Where(g => !string.IsNullOrEmpty(g))
            .Distinct()
            .FirstOrDefault();

        album.Artists = tracks.SelectMany(t => t.Artists)
            .Where(artistName => !string.IsNullOrEmpty(artistName))
            .Distinct()
            .ToList();

        if (album.Artists.Count == 0)
        {
            album.Artists = new List<string> { "Attrition OST" };
        }

        if (!album.IsCoverUserDefined)
        {
            var firstTrackWithCover = tracks.FirstOrDefault(t => !string.IsNullOrEmpty(t.CoverPath));
            if (firstTrackWithCover != null)
            {
                album.CoverPath = firstTrackWithCover.CoverPath;
            }
            else
            {
                album.CoverPath = null;
            }
        }

        await _albumRepo.UpdateAsync(album);
    }

    public async Task<IEnumerable<MusicTrackDto>> GetTracksAsync(int? albumId)
    {
        Expression<Func<MusicTrack, bool>>? filter = null;
        if (albumId.HasValue) filter = t => t.AlbumId == albumId.Value;

        var (tracks, _) = await _trackRepo.GetPagedAsync(
            1, int.MaxValue, filter,
            q => q.OrderBy(t => t.AlbumId).ThenBy(t => t.TrackNumber)
        );

        var dtos = new List<MusicTrackDto>();
        foreach (var t in tracks)
        {
            var album = await _albumRepo.GetByIdAsync(t.AlbumId);
            dtos.Add(new MusicTrackDto(
                t.TrackId, t.AlbumId, t.Title, t.Slug, t.TrackNumber, t.Artists,
                t.Duration, t.Genre, t.CoverPath, t.PlayCount, t.IsFeatured, t.FileSize ?? 0,
                album?.Title, album?.CoverPath
            ));
        }
        return dtos;
    }

    public async Task<FeaturedTracksResponse> GetFeaturedTracksAsync()
    {
        var lastMonthLimit = DateTime.UtcNow.AddDays(-30);

        var (featuredTracks, _) = await _trackRepo.GetPagedAsync(
            1, 10, t => t.CreatedAt >= lastMonthLimit,
            q => q.OrderByDescending(t => t.PlayCount)
        );

        if (featuredTracks.Count < 10)
        {
            var excludedIds = featuredTracks.Select(t => t.TrackId).ToList();
            var (fallbackTracks, _) = await _trackRepo.GetPagedAsync(
                1, 10 - featuredTracks.Count, t => !excludedIds.Contains(t.TrackId),
                q => q.OrderByDescending(t => t.PlayCount)
            );
            featuredTracks.AddRange(fallbackTracks);
        }

        var sortedTracks = featuredTracks.OrderByDescending(t => t.PlayCount).ToList();

        var featuredTrackDtos = new List<MusicTrackDto>();
        foreach (var t in sortedTracks)
        {
            var album = await _albumRepo.GetByIdAsync(t.AlbumId);
            featuredTrackDtos.Add(new MusicTrackDto(
                t.TrackId, t.AlbumId, t.Title, t.Slug, t.TrackNumber, t.Artists,
                t.Duration, t.Genre, t.CoverPath, t.PlayCount, t.IsFeatured, t.FileSize ?? 0,
                album?.Title, album?.CoverPath
            ));
        }

        var albums = await _albumRepo.GetAllAsync();
        var newestAlbums = new List<NewestAlbumDto>();

        foreach (var a in albums)
        {
            var (tracks, _) = await _trackRepo.GetPagedAsync(1, int.MaxValue, t => t.AlbumId == a.AlbumId);
            if (tracks.Count > 0)
            {
                var newestTrackAddedAt = tracks.Max(t => t.CreatedAt);
                newestAlbums.Add(new NewestAlbumDto(
                    a.AlbumId,
                    a.Title,
                    a.CoverPath,
                    a.Artists,
                    tracks.Count,
                    newestTrackAddedAt
                ));
            }
        }

        var sortedNewestAlbums = newestAlbums
            .OrderByDescending(na => na.NewestTrackAddedAt)
            .Take(5)
            .ToList();

        return new FeaturedTracksResponse(featuredTrackDtos, sortedNewestAlbums);
    }

    public async Task<(string? filePath, bool trackExists)> GetTrackStreamInfoAsync(int id)
    {
        var track = await _trackRepo.GetByIdAsync(id);
        if (track == null) return (null, false);

        var filePath = Path.Combine(GetUploadPath(), "music", track.FilePath);
        if (!System.IO.File.Exists(filePath)) return (null, true);

        return (filePath, true);
    }

    public async Task<bool> IncrementPlayCountAsync(int id)
    {
        var track = await _trackRepo.GetByIdAsync(id);
        if (track == null) return false;

        track.PlayCount++;
        await _trackRepo.UpdateAsync(track);
        return true;
    }

    public async Task<(bool success, string? error, ScanTrackResponse? data)> ScanTrackAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return (false, "Audio file is required", null);

        if (file.Length > 100 * 1024 * 1024)
            return (false, "File must be under 100MB", null);

        var tempDir = Path.Combine(GetUploadPath(), "music", "temp");
        Directory.CreateDirectory(tempDir);

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
        catch { }

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
        catch { }

        if (!string.IsNullOrEmpty(albumTitle))
        {
            var normAlbumTitle = albumTitle.Trim().ToLower();
            var (albums, _) = await _albumRepo.GetPagedAsync(1, 1, a => a.Title.ToLower() == normAlbumTitle);
            var album = albums.FirstOrDefault();
            if (album != null)
            {
                var normTitle = title.Trim().ToLower();
                var (candidateTracks, _) = await _trackRepo.GetPagedAsync(1, int.MaxValue, t => t.AlbumId == album.AlbumId && t.Title.Trim().ToLower() == normTitle);

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

        return (true, null, new ScanTrackResponse(
            tempFileKey,
            title,
            albumTitle,
            artists,
            genre,
            trackNumber,
            duration,
            tempCoverPath
        ));
    }

    public async Task<(bool success, string? error, MusicTrackDto? data)> UploadTrackAsync(UploadTrackRequest req)
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

            if (req.File.Length > 100 * 1024 * 1024)
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
        catch { }

        MusicAlbum? album = null;
        if (req.AlbumId.HasValue)
        {
            album = await _albumRepo.GetByIdAsync(req.AlbumId.Value);
        }
        else if (!string.IsNullOrEmpty(albumTitle))
        {
            var normAlbumTitle = albumTitle.Trim().ToLower();
            var (albums, _) = await _albumRepo.GetPagedAsync(1, 1, a => a.Title.ToLower() == normAlbumTitle);
            album = albums.FirstOrDefault();
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
                await _albumRepo.AddAsync(album);
            }
        }

        if (album == null)
        {
            if (string.IsNullOrEmpty(req.TempFileKey) && System.IO.File.Exists(tempAudioPath))
                System.IO.File.Delete(tempAudioPath);

            return (false, "Album ID is required if track has no album tag", null);
        }

        var normTitle = title.Trim().ToLower();
        var (candidateTracks, _) = await _trackRepo.GetPagedAsync(1, int.MaxValue, t => t.AlbumId == album.AlbumId && t.Title.Trim().ToLower() == normTitle);

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

        await _trackRepo.AddAsync(track);
        await RescanAlbumAggregatesAsync(album.AlbumId);

        return (true, null, new MusicTrackDto(
            track.TrackId, track.AlbumId, track.Title, track.Slug, track.TrackNumber, track.Artists,
            track.Duration, track.Genre, track.CoverPath, track.PlayCount, track.IsFeatured, track.FileSize ?? 0,
            album.Title, album.CoverPath
        ));
    }

    public async Task<(bool success, string? error, MusicTrackDto? data)> UpdateTrackAsync(int id, UpdateTrackRequest req)
    {
        var track = await _trackRepo.GetByIdAsync(id);
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

        await _trackRepo.UpdateAsync(track);
        await RescanAlbumAggregatesAsync(track.AlbumId);

        var album = await _albumRepo.GetByIdAsync(track.AlbumId);

        return (true, null, new MusicTrackDto(
            track.TrackId, track.AlbumId, track.Title, track.Slug, track.TrackNumber, track.Artists,
            track.Duration, track.Genre, track.CoverPath, track.PlayCount, track.IsFeatured, track.FileSize ?? 0,
            album?.Title, album?.CoverPath
        ));
    }

    public async Task<bool> DeleteTrackAsync(int id)
    {
        var track = await _trackRepo.GetByIdAsync(id);
        if (track == null) return false;

        var filePath = Path.Combine(GetUploadPath(), "music", track.FilePath);
        if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);

        if (!string.IsNullOrEmpty(track.CoverPath))
        {
            var coverFullPath = Path.Combine(GetUploadPath(), track.CoverPath.TrimStart('/'));
            if (System.IO.File.Exists(coverFullPath)) System.IO.File.Delete(coverFullPath);
        }

        var albumId = track.AlbumId;
        await _trackRepo.DeleteAsync(track);
        await RescanAlbumAggregatesAsync(albumId);

        return true;
    }
}
