using BuildingBlocks.Contracts;
using Character.Service.DTOs;
using Character.Service.Models;
using Character.Service.Repositories;

namespace Character.Service.Services;

public class CharacterService : ICharacterService
{
    private readonly ICharacterRepository _repo;

    public CharacterService(ICharacterRepository repo) => _repo = repo;

    public async Task<List<CharacterSummaryDto>> GetByOwnerAsync(Guid ownerId)
    {
        var characters = await _repo.GetByOwnerWithSnapshotsAsync(ownerId);
        return characters.Select(ToSummary).ToList();
    }

    public async Task<CharacterDetailDto?> GetDetailAsync(Guid id)
    {
        var character = await _repo.GetWithSnapshotsAsync(id);
        return character == null ? null : ToDetail(character);
    }

    public async Task<List<AdminCharacterDto>> GetAllAsync()
    {
        var characters = await _repo.GetAllWithSnapshotsAsync();
        // OwnerUsername resolved from Identity is left null in the stub; the frame displays the GUID.
        return characters.Select(c => new AdminCharacterDto(
            c.Id, c.OwnerId, null, c.Name, c.Archetype, c.UpdatedAt, LatestSnapshot(c))).ToList();
    }

    public async Task<ApiResponse<CharacterDetailDto>> IngestSnapshotAsync(SnapshotIngestRequest request)
    {
        // Resolve the target character: by id if given, else by (owner, name), else create.
        CharacterEntity? character = null;
        if (request.CharacterId.HasValue)
            character = await _repo.GetWithSnapshotsAsync(request.CharacterId.Value);
        character ??= await _repo.FindByOwnerAndNameAsync(request.OwnerId, request.Name);

        var snapshot = new CharacterSnapshot
        {
            Level = request.Level,
            Hp = request.Hp,
            MaxHp = request.MaxHp,
            Gold = request.Gold,
            IsAlive = request.IsAlive,
            RoomCode = request.RoomCode,
            EventType = request.EventType,
            PlaytimeSeconds = request.PlaytimeSeconds,
            CapturedAt = DateTime.UtcNow
        };

        if (character == null)
        {
            character = new CharacterEntity
            {
                OwnerId = request.OwnerId,
                Name = request.Name,
                Archetype = request.Archetype,
                Snapshots = new List<CharacterSnapshot> { snapshot }
            };
            await _repo.AddAsync(character);
        }
        else
        {
            character.Archetype = request.Archetype;
            character.UpdatedAt = DateTime.UtcNow;
            character.Snapshots.Add(snapshot);
            await _repo.UpdateAsync(character);
        }

        return ApiResponse<CharacterDetailDto>.Ok(ToDetail(character));
    }

    public Task<int> CountAsync() => _repo.CountAsync();

    // ─── mapping ───
    private static SnapshotDto ToSnapshotDto(CharacterSnapshot s) => new(
        s.Level, s.Hp, s.MaxHp, s.Gold, s.IsAlive, s.RoomCode, s.EventType, s.PlaytimeSeconds, s.CapturedAt);

    private static SnapshotDto? LatestSnapshot(CharacterEntity c) =>
        c.Snapshots.Count == 0 ? null
            : ToSnapshotDto(c.Snapshots.OrderByDescending(s => s.CapturedAt).First());

    private static CharacterSummaryDto ToSummary(CharacterEntity c) => new(
        c.Id, c.OwnerId, c.Name, c.Archetype, c.CreatedAt, c.UpdatedAt,
        c.Snapshots.Count, LatestSnapshot(c));

    private static CharacterDetailDto ToDetail(CharacterEntity c) => new(
        c.Id, c.OwnerId, c.Name, c.Archetype, c.CreatedAt, c.UpdatedAt,
        c.Snapshots.OrderByDescending(s => s.CapturedAt).Select(ToSnapshotDto).ToList());
}
