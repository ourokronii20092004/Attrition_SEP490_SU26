using Music.Service.DTOs;

namespace Music.Service.Services.Interface;

public interface IFavoriteService
{
    Task<IEnumerable<FavoriteTrackDto>> GetFavoritesAsync(Guid userId);
    Task<IEnumerable<int>> GetFavoriteIdsAsync(Guid userId);
    Task<(bool success, bool isFavorited, string? error)> ToggleFavoriteAsync(Guid userId, int trackId);
}
