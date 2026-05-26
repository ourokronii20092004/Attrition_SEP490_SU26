using Attrition.API.DTOs;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Attrition.API.Services;

public interface ITrackService
{
    Task<IEnumerable<MusicTrackDto>> GetTracksAsync(int? albumId);
    Task<FeaturedTracksResponse> GetFeaturedTracksAsync();
    Task<(string? filePath, bool trackExists)> GetTrackStreamInfoAsync(int id);
    Task<bool> IncrementPlayCountAsync(int id);
    Task<(bool success, string? error, ScanTrackResponse? data)> ScanTrackAsync(IFormFile file);
    Task<(bool success, string? error, MusicTrackDto? data)> UploadTrackAsync(UploadTrackRequest req);
    Task<(bool success, string? error, MusicTrackDto? data)> UpdateTrackAsync(int id, UpdateTrackRequest req);
    Task<bool> DeleteTrackAsync(int id);
}
