using Attrition.API.DTOs;
using Attrition.API.Models;
using Attrition.API.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Attrition.API.Services;

public class FavoriteService : IFavoriteService
{
    private readonly IRepository<MusicAlbum> _albumRepo;
    private readonly IRepository<MusicTrack> _trackRepo;
    private readonly IRepository<UserFavorite> _favoriteRepo;

    public FavoriteService(
        IRepository<MusicAlbum> albumRepo,
        IRepository<MusicTrack> trackRepo,
        IRepository<UserFavorite> favoriteRepo)
    {
        _albumRepo = albumRepo;
        _trackRepo = trackRepo;
        _favoriteRepo = favoriteRepo;
    }

    public async Task<IEnumerable<FavoriteTrackDto>> GetFavoritesAsync(Guid userId)
    {
        var (favorites, _) = await _favoriteRepo.GetPagedAsync(
            1, int.MaxValue, f => f.UserId == userId,
            q => q.OrderByDescending(f => f.AddedAt)
        );

        var dtos = new List<FavoriteTrackDto>();
        foreach (var f in favorites)
        {
            var track = await _trackRepo.GetByIdAsync(f.TrackId);
            if (track != null)
            {
                var album = await _albumRepo.GetByIdAsync(track.AlbumId);
                dtos.Add(new FavoriteTrackDto(
                    track.TrackId, track.AlbumId, track.Title, track.Slug, track.Artists,
                    track.TrackNumber, track.Duration, track.Genre, track.CoverPath, track.PlayCount,
                    album?.Title ?? string.Empty, album?.CoverPath ?? string.Empty,
                    f.AddedAt
                ));
            }
        }
        return dtos;
    }

    public async Task<IEnumerable<int>> GetFavoriteIdsAsync(Guid userId)
    {
        var (favorites, _) = await _favoriteRepo.GetPagedAsync(1, int.MaxValue, f => f.UserId == userId);
        return favorites.Select(f => f.TrackId);
    }

    public async Task<(bool success, bool isFavorited, string? error)> ToggleFavoriteAsync(Guid userId, int trackId)
    {
        var (existingList, _) = await _favoriteRepo.GetPagedAsync(1, 1, f => f.UserId == userId && f.TrackId == trackId);
        var existing = existingList.FirstOrDefault();

        if (existing != null)
        {
            await _favoriteRepo.DeleteAsync(existing);
            return (true, false, null);
        }

        var track = await _trackRepo.GetByIdAsync(trackId);
        if (track == null) return (false, false, "Track not found");

        var favorite = new UserFavorite { UserId = userId, TrackId = trackId };
        await _favoriteRepo.AddAsync(favorite);
        return (true, true, null);
    }
}
