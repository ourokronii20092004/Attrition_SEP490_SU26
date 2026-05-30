using BuildingBlocks.Contracts;
using BuildingBlocks.Persistence;
using BuildingBlocks.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Music.Service.DTOs;
using Music.Service.Models;

namespace Music.Service.Services;

public class AlbumService : IAlbumService
{
    private readonly IRepository<MusicAlbum> _albumRepo;
    private readonly IRepository<MusicTrack> _trackRepo;
    private readonly ILogger<AlbumService> _logger;
    private readonly string _uploadPath;
    private readonly string _publicPrefix;

    public AlbumService(IRepository<MusicAlbum> albumRepo, IRepository<MusicTrack> trackRepo,
        IConfiguration config, ILogger<AlbumService> logger)
    {
        _albumRepo = albumRepo;
        _trackRepo = trackRepo;
        _logger = logger;
        _uploadPath = config["FileUpload:UploadPath"] ?? "/app/uploads";
        _publicPrefix = config["FileUpload:PublicPrefix"] ?? "/api/music/media";
    }

    public async Task<IEnumerable<MusicAlbumDto>> GetAlbumsAsync()
    {
        var albums = await _albumRepo.GetAllAsync();
        return albums.OrderByDescending(a => a.CreatedAt).Select(ToDto);
    }

    public async Task<PaginatedResponse<MusicAlbumDto>> GetAlbumsPagedAsync(int page, int pageSize)
    {
        var (items, total) = await _albumRepo.GetPagedAsync(page, pageSize, null,
            q => q.OrderByDescending(a => a.CreatedAt));
        return new PaginatedResponse<MusicAlbumDto>(items.Select(ToDto).ToList(), total, page, pageSize);
    }

    public async Task<AlbumDetailDto?> GetAlbumAsync(int id)
    {
        var album = await _albumRepo.GetByIdAsync(id);
        if (album == null) return null;

        var tracks = await _trackRepo.ListAsync(t => t.AlbumId == id,
            q => q.OrderBy(t => t.TrackNumber));

        return new AlbumDetailDto(album.AlbumId, album.Title, album.Slug, album.Artists, album.Description,
            album.CoverPath, album.IsCoverUserDefined, album.ReleaseDate, album.AlbumType, album.Genre,
            album.TrackCount, album.TotalDuration, album.CreatedAt,
            tracks.Select(t => new MusicTrackDto(t.TrackId, t.AlbumId, t.Title, t.Slug, t.TrackNumber,
                t.Artists, t.Duration, t.Genre, t.CoverPath, t.PlayCount, t.IsFeatured, t.FileSize ?? 0)));
    }

    public async Task<MusicAlbumDto> CreateAlbumAsync(CreateAlbumRequest req)
    {
        var parsed = MusicHelpers.ParseArtists(req.Artists);
        var album = new MusicAlbum
        {
            Title = req.Title,
            Slug = await UniqueAlbumSlugAsync(req.Title, null),
            Artists = parsed.Count > 0 ? parsed : new List<string> { "Attrition OST" },
            Description = req.Description,
            Genre = req.Genre,
            AlbumType = req.AlbumType ?? "soundtrack",
            ReleaseDate = req.ReleaseDate,
            SortOrder = req.SortOrder
        };
        await _albumRepo.AddAsync(album);
        return ToDto(album);
    }

    private async Task<string> UniqueAlbumSlugAsync(string title, int? selfId) =>
        await SlugHelper.GenerateUniqueSlugAsync(title, async slug =>
            await _albumRepo.CountAsync(a => a.Slug == slug && (selfId == null || a.AlbumId != selfId)) > 0);

    public async Task<MusicAlbumDto?> UpdateAlbumAsync(int id, CreateAlbumRequest req)
    {
        var album = await _albumRepo.GetByIdAsync(id);
        if (album == null) return null;

        album.Title = req.Title;
        album.Slug = await UniqueAlbumSlugAsync(req.Title, id);
        if (req.Artists != null)
        {
            var parsed = MusicHelpers.ParseArtists(req.Artists);
            if (parsed.Count > 0) album.Artists = parsed;
        }
        album.Description = req.Description;
        album.Genre = req.Genre ?? album.Genre;
        album.AlbumType = req.AlbumType ?? album.AlbumType;
        album.ReleaseDate = req.ReleaseDate;
        album.SortOrder = req.SortOrder;

        await _albumRepo.UpdateAsync(album);
        return ToDto(album);
    }

    private static MusicAlbumDto ToDto(MusicAlbum a) => new(
        a.AlbumId, a.Title, a.Slug, a.Artists, a.Description, a.CoverPath, a.IsCoverUserDefined,
        a.ReleaseDate, a.AlbumType, a.Genre, a.TrackCount, a.TotalDuration, a.CreatedAt);

    public async Task<bool> DeleteAlbumAsync(int id)
    {
        var album = await _albumRepo.GetByIdAsync(id);
        if (album == null) return false;

        var tracks = await _trackRepo.ListAsync(t => t.AlbumId == id);
        foreach (var track in tracks)
        {
            var filePath = Path.Combine(_uploadPath, "music", track.FilePath);
            // Continue on individual failures so one locked file doesn't strand the rest mid-loop.
            await SafeFileOperations.SafeDeleteAsync(filePath, _logger);
        }

        if (!string.IsNullOrEmpty(album.CoverPath))
            await DeletePublicFileAsync(album.CoverPath);

        await _albumRepo.DeleteAsync(album);
        return true;
    }

    public async Task<(bool success, string? error, string? coverPath)> UploadAlbumCoverAsync(int id, IFormFile file)
    {
        var album = await _albumRepo.GetByIdAsync(id);
        if (album == null) return (false, "Album not found", null);
        if (file.Length > 10 * 1024 * 1024) return (false, "Cover image must be under 10MB", null);
        if (!MusicHelpers.IsAllowedImageExtension(file.FileName))
            return (false, "Cover must be an image (.jpg, .png, .webp)", null);
        await using (var check = file.OpenReadStream())
        {
            if (!await MusicHelpers.LooksLikeImageAsync(check))
                return (false, "Cover file is not a valid image", null);
        }

        var coverDir = Path.Combine(_uploadPath, "music", "covers");
        Directory.CreateDirectory(coverDir);

        var ext = Path.GetExtension(file.FileName);
        var fileName = $"album-{id}{ext}";
        var filePath = Path.Combine(coverDir, fileName);
        await using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        album.CoverPath = $"{_publicPrefix}/music/covers/{fileName}";
        album.IsCoverUserDefined = true;
        await _albumRepo.UpdateAsync(album);

        return (true, null, album.CoverPath);
    }

    public Task<int> CountAsync() => _albumRepo.CountAsync();

    private async Task DeletePublicFileAsync(string publicUrl)
    {
        var prefix = _publicPrefix + "/";
        var rel = publicUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? publicUrl[prefix.Length..]
            : publicUrl.TrimStart('/');
        var full = Path.Combine(_uploadPath, rel.Replace('/', Path.DirectorySeparatorChar));
        await SafeFileOperations.SafeDeleteAsync(full, _logger);
    }
}
