using Attrition.API.DTOs;
using Attrition.API.Models;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Attrition.API.Services;

public interface IAlbumService
{
    Task<IEnumerable<MusicAlbumDto>> GetAlbumsAsync();
    Task<AlbumDetailDto?> GetAlbumAsync(int id);
    Task<MusicAlbum> CreateAlbumAsync(CreateAlbumRequest req);
    Task<MusicAlbum?> UpdateAlbumAsync(int id, CreateAlbumRequest req);
    Task<bool> DeleteAlbumAsync(int id);
    Task<(bool success, string? error, string? coverPath)> UploadAlbumCoverAsync(int id, IFormFile file);
}
