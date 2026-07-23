using Music.Service.DTOs;

namespace Music.Service.Services.Interface;

public interface ITrackService
{
    Task<IEnumerable<MusicTrackDto>> GetTracksAsync(int? albumId);
    Task<BuildingBlocks.Contracts.PaginatedResponse<MusicTrackDto>> GetTracksPagedAsync(int? albumId, int page, int pageSize);
    Task<FeaturedTracksResponse> GetFeaturedTracksAsync();
    Task<(string? filePath, bool trackExists)> GetTrackStreamInfoAsync(int id);
    Task<(string? filePath, string fileName, bool trackExists)> GetTrackDownloadInfoAsync(int id);
    Task<bool> IncrementPlayCountAsync(int id);
    Task<(bool success, string? error, ScanTrackResponse? data)> ScanTrackAsync(Microsoft.AspNetCore.Http.IFormFile file);
    Task<(bool success, string? error, MusicTrackDto? data)> UploadTrackAsync(UploadTrackRequest req);
    Task<(bool success, string? error, MusicTrackDto? data)> UpdateTrackAsync(int id, UpdateTrackRequest req);
    Task<bool> DeleteTrackAsync(int id);
    Task<int> CountAsync();
}
