using Attrition.API.DTOs;
using Attrition.API.Models;
using Attrition.API.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Attrition.API.Services;

public class AlbumService : IAlbumService
{
    private readonly IRepository<MusicAlbum> _albumRepo;
    private readonly IRepository<MusicTrack> _trackRepo;
    private readonly IConfiguration _config;

    public AlbumService(
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

    public async Task<IEnumerable<MusicAlbumDto>> GetAlbumsAsync()
    {
        var albums = await _albumRepo.GetAllAsync();
        return albums.OrderByDescending(a => a.CreatedAt)
            .Select(a => new MusicAlbumDto(
                a.AlbumId, a.Title, a.Slug, a.Artists, a.Description,
                a.CoverPath, a.IsCoverUserDefined, a.ReleaseDate, a.AlbumType, a.Genre,
                a.TrackCount, a.TotalDuration, a.CreatedAt
            ));
    }

    public async Task<AlbumDetailDto?> GetAlbumAsync(int id)
    {
        var album = await _albumRepo.GetByIdAsync(id);
        if (album == null) return null;

        var (tracks, _) = await _trackRepo.GetPagedAsync(
            1, int.MaxValue, t => t.AlbumId == id,
            q => q.OrderBy(t => t.TrackNumber)
        );

        return new AlbumDetailDto(
            album.AlbumId, album.Title, album.Slug, album.Artists, album.Description,
            album.CoverPath, album.IsCoverUserDefined, album.ReleaseDate, album.AlbumType, album.Genre,
            album.TrackCount, album.TotalDuration, album.CreatedAt,
            tracks.Select(t => new MusicTrackDto(
                t.TrackId, t.AlbumId, t.Title, t.Slug, t.TrackNumber, t.Artists,
                t.Duration, t.Genre, t.CoverPath, t.PlayCount, t.IsFeatured, t.FileSize ?? 0
            ))
        );
    }

    public async Task<MusicAlbum> CreateAlbumAsync(CreateAlbumRequest req)
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

        return await _albumRepo.AddAsync(album);
    }

    public async Task<MusicAlbum?> UpdateAlbumAsync(int id, CreateAlbumRequest req)
    {
        var album = await _albumRepo.GetByIdAsync(id);
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

        await _albumRepo.UpdateAsync(album);
        return album;
    }

    public async Task<bool> DeleteAlbumAsync(int id)
    {
        var album = await _albumRepo.GetByIdAsync(id);
        if (album == null) return false;

        var (tracks, _) = await _trackRepo.GetPagedAsync(1, int.MaxValue, t => t.AlbumId == id);

        foreach (var track in tracks)
        {
            var filePath = Path.Combine(GetUploadPath(), "music", track.FilePath);
            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
        }

        if (!string.IsNullOrEmpty(album.CoverPath))
        {
            var coverPath = Path.Combine(GetUploadPath(), album.CoverPath.TrimStart('/'));
            if (System.IO.File.Exists(coverPath)) System.IO.File.Delete(coverPath);
        }

        await _albumRepo.DeleteAsync(album);
        return true;
    }

    public async Task<(bool success, string? error, string? coverPath)> UploadAlbumCoverAsync(int id, IFormFile file)
    {
        var album = await _albumRepo.GetByIdAsync(id);
        if (album == null) return (false, "Album not found", null);

        if (file.Length > 10 * 1024 * 1024)
            return (false, "Cover image must be under 10MB", null);

        var coverDir = Path.Combine(GetUploadPath(), "music", "covers");
        Directory.CreateDirectory(coverDir);

        var ext = Path.GetExtension(file.FileName);
        var fileName = $"album-{id}{ext}";
        var filePath = Path.Combine(coverDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        album.CoverPath = $"/uploads/music/covers/{fileName}";
        album.IsCoverUserDefined = true;
        await _albumRepo.UpdateAsync(album);

        return (true, null, album.CoverPath);
    }
}
