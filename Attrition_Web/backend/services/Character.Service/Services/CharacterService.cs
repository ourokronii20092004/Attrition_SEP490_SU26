using BuildingBlocks.Contracts;
using Character.Service.Clients;
using Character.Service.DTOs;
using Character.Service.Models;
using Character.Service.Repositories;

namespace Character.Service.Services;

public class CharacterService : ICharacterService
{
    private readonly ICharacterRepository _repo;
    private readonly IdentityClient _identity;

    public CharacterService(ICharacterRepository repo, IdentityClient identity)
    {
        _repo = repo;
        _identity = identity;
    }

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

    public async Task<PaginatedResponse<AdminCharacterDto>> GetAllAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var (characters, total) = await _repo.GetPagedWithSnapshotsAsync(page, pageSize);
        var usernames = await _identity.ResolveUsernamesAsync(
            characters.Select(c => c.OwnerId).ToList(), ct);
        var items = characters.Select(c => new AdminCharacterDto(
            c.Id, c.OwnerId,
            usernames.GetValueOrDefault(c.OwnerId),
            c.Name, c.Archetype, c.UpdatedAt, LatestSnapshot(c))).ToList();
        return new PaginatedResponse<AdminCharacterDto>(items, total, page, pageSize);
    }

    public async Task<ApiResponse<CharacterDetailDto>> IngestSnapshotAsync(SnapshotIngestRequest request)
    {
        // Guard inputs so a malformed body (or one that bypassed validation) fails as a clean 400
        // rather than throwing deeper in. Makes the controller's BadRequest branch reachable (CH-3).
        if (request.OwnerId == Guid.Empty)
            return ApiResponse<CharacterDetailDto>.Fail("OwnerId is required.");
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResponse<CharacterDetailDto>.Fail("Character name is required.");

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
                InventoryJson = request.InventoryJson,
                EquipmentJson = request.EquipmentJson,
                QuestsJson = request.QuestsJson,
                Snapshots = new List<CharacterSnapshot> { snapshot }
            };
            // Race-safe insert: a concurrent snapshot for the same new (owner, name) hits the unique
            // index. On a lost race, fall back to updating the row the winner created (CH-1).
            if (!await _repo.TryAddAsync(character))
            {
                character = await _repo.FindByOwnerAndNameAsync(request.OwnerId, request.Name);
                if (character == null)
                    return ApiResponse<CharacterDetailDto>.Fail("Could not persist the character snapshot. Please retry.");
                return await ApplySnapshotUpdateAsync(character, request, snapshot);
            }
        }
        else
        {
            return await ApplySnapshotUpdateAsync(character, request, snapshot);
        }

        return ApiResponse<CharacterDetailDto>.Ok(ToDetail(character));
    }

    private async Task<ApiResponse<CharacterDetailDto>> ApplySnapshotUpdateAsync(
        CharacterEntity character, SnapshotIngestRequest request, CharacterSnapshot snapshot)
    {
        character.Archetype = request.Archetype;
        character.UpdatedAt = DateTime.UtcNow;
        // Giữ giá trị cũ nếu client không gửi (null) → tránh xoá nhầm inventory khi snapshot không kèm.
        character.InventoryJson = request.InventoryJson ?? character.InventoryJson;
        character.EquipmentJson = request.EquipmentJson ?? character.EquipmentJson;
        character.QuestsJson = request.QuestsJson ?? character.QuestsJson;
        character.Snapshots.Add(snapshot);
        try
        {
            await _repo.UpdateAsync(character);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            // The character was deleted between fetch and save.
            return ApiResponse<CharacterDetailDto>.Fail("The character was modified or removed by another request. Please retry.");
        }
        return ApiResponse<CharacterDetailDto>.Ok(ToDetail(character));
    }

    public async Task<ApiResponse> DeleteAsync(Guid id, Guid ownerId, bool isAdmin)
    {
        var character = await _repo.GetWithSnapshotsAsync(id);
        if (character == null) return ApiResponse.Fail("Character not found.");
        if (character.OwnerId != ownerId && !isAdmin) return ApiResponse.Fail("You do not have permission to delete this character.");

        await _repo.DeleteAsync(character);
        return ApiResponse.Ok();
    }

    public Task<int> CountAsync() => _repo.CountAsync();

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
        c.Snapshots.OrderByDescending(s => s.CapturedAt).Select(ToSnapshotDto).ToList(),
        c.InventoryJson, c.EquipmentJson, c.QuestsJson);
}
