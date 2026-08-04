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
    private readonly ISessionRepository _sessions;
    private readonly ILogger<CharacterService> _logger;

    public CharacterService(
        ICharacterRepository repo,
        IdentityClient identity,
        ISessionRepository sessions,
        ILogger<CharacterService> logger)
    {
        _repo = repo;
        _identity = identity;
        _sessions = sessions;
        _logger = logger;
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

    // ── Save files ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ownership guard shared by every save endpoint. Returns the character's owner id when the
    /// caller may act on it, or an error describing why not. Admins pass, but the owner id is still
    /// returned so callers can tell whose data they are touching.
    /// </summary>
    private async Task<(Guid OwnerId, string Name)?> AuthorizeCharacterAsync(Guid characterId, Guid callerId, bool isAdmin)
    {
        var row = await _repo.GetOwnerAndNameAsync(characterId);
        if (row is not { } found) return null;
        if (found.OwnerId != callerId && !isAdmin) return null;
        return found;
    }

    public async Task<ApiResponse<SaveListDto>> GetSavesAsync(
        Guid characterId, Guid callerId, bool isAdmin, int page, int pageSize)
    {
        if (await AuthorizeCharacterAsync(characterId, callerId, isAdmin) is null)
            return ApiResponse<SaveListDto>.Fail("Character not found.");

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var (items, total) = await _sessions.GetSavesAsync(characterId, page, pageSize);

        // "Current" is the newest save overall, not the newest on this page — page 3 must not
        // relabel its first row as current.
        var newestId = items.Count > 0 && page == 1 ? items[0].Id : (await NewestSaveIdAsync(characterId));

        var dtos = items.Select(x => new SaveListItemDto(
            x.Id, x.SessionId, x.RoomCode, x.CurrentScene, x.EventType,
            x.CurrentLevel, x.Vitals.CurrentHp, x.Vitals.MaxHp,
            x.DeathCount, x.PlaytimeSeconds, x.IsAlive, x.CapturedAt,
            x.Id == newestId)).ToList();

        return ApiResponse<SaveListDto>.Ok(new SaveListDto(dtos, total, page, pageSize));
    }

    private async Task<long?> NewestSaveIdAsync(Guid characterId)
    {
        var (first, _) = await _sessions.GetSavesAsync(characterId, 1, 1);
        return first.Count > 0 ? first[0].Id : null;
    }

    public async Task<ApiResponse<SaveDetailDto>> GetSaveAsync(
        Guid characterId, long saveId, Guid callerId, bool isAdmin)
    {
        if (await AuthorizeCharacterAsync(characterId, callerId, isAdmin) is null)
            return ApiResponse<SaveDetailDto>.Fail("Character not found.");

        var save = await _sessions.GetSaveAsync(saveId);
        // Check the save belongs to this character: a valid save id from someone else's character
        // must not be readable by passing your own character id.
        if (save == null || save.CharacterId != characterId)
            return ApiResponse<SaveDetailDto>.Fail("Save not found.");

        var newestId = await NewestSaveIdAsync(characterId);

        return ApiResponse<SaveDetailDto>.Ok(new SaveDetailDto(
            save.Id, save.CharacterId, save.SessionId, save.RoomCode, save.CurrentScene,
            save.EventType, save.PlayerRole,
            save.CurrentLevel, save.CurrentExp, save.DeathCount, save.PlaytimeSeconds, save.IsAlive,
            save.AllocatedPointsJson,
            save.Vitals.MaxHp, save.Vitals.CurrentHp, save.Vitals.MaxMana, save.Vitals.CurrentMana,
            save.Vitals.MaxStamina,
            save.Combat.AttackSpeed,
            save.Combat.PotionMaxFlasks, save.Combat.PotionMaxManaFlasks,
            save.Combat.HealthCharges, save.Combat.ManaCharges,
            save.Combat.Ad, save.Combat.Ap, save.Combat.Def, save.Combat.Res,
            save.Position.PosX, save.Position.PosY, save.Position.PosZ,
            save.Position.LastRestPointId,
            save.InventoryJson,
            save.CapturedAt,
            save.Id == newestId));
    }

    public async Task<ApiResponse<DeleteSaveResultDto>> DeleteSaveAsync(
        Guid characterId, long saveId, Guid callerId, bool isAdmin, bool alsoRollBackWorldState)
    {
        if (await AuthorizeCharacterAsync(characterId, callerId, isAdmin) is null)
            return ApiResponse<DeleteSaveResultDto>.Fail("Character not found.");

        var save = await _sessions.GetSaveAsync(saveId);
        if (save == null || save.CharacterId != characterId)
            return ApiResponse<DeleteSaveResultDto>.Fail("Save not found.");

        // A character with no saves has no defined state to load, so the last one is not deletable.
        var total = await _sessions.CountSavesAsync(characterId);
        if (total <= 1)
            return ApiResponse<DeleteSaveResultDto>.Fail(
                "This is the only save for this character. Delete the character instead if you want to remove it.");

        var newestId = await NewestSaveIdAsync(characterId);
        var wasCurrent = save.Id == newestId;

        // Only the newest save's deletion rolls anything back: removing a middle save just prunes
        // history, because live state already reflects something newer.
        var rollBackTo = wasCurrent
            ? await _sessions.GetNewestSaveExcludingAsync(characterId, saveId)
            : null;

        // World rollback is opt-in, owner-only, and only meaningful alongside a character rollback.
        var rolledBackWorld = false;
        if (alsoRollBackWorldState && wasCurrent && save.SessionId is { } sessionId)
        {
            var roomOwner = await _sessions.GetOwnerIdAsync(sessionId);
            if (roomOwner == callerId || isAdmin)
            {
                // Match the snapshot to the save we are rolling back TO, not the one being deleted:
                // the target is the state the room should return to.
                var target = rollBackTo != null
                    ? await _sessions.GetRoomStateAtOrBeforeAsync(sessionId, rollBackTo.CapturedAt)
                    : null;
                if (target != null)
                {
                    try
                    {
                        await _sessions.RestoreRoomStateAsync(sessionId, target);
                        rolledBackWorld = true;
                    }
                    catch (Exception ex)
                    {
                        // The character rollback below is the primary action and still proceeds; the
                        // response reports rolledBackWorldState=false so the UI can say so honestly.
                        _logger.LogWarning(ex, "World-state rollback failed for session {SessionId}", sessionId);
                    }
                }
            }
        }

        var ok = await _sessions.DeleteSaveAndRollBackAsync(save, rollBackTo);
        if (!ok) return ApiResponse<DeleteSaveResultDto>.Fail("Could not delete that save. Please try again.");

        return ApiResponse<DeleteSaveResultDto>.Ok(new DeleteSaveResultDto(
            WasCurrent: wasCurrent,
            RolledBackCharacter: wasCurrent && rollBackTo != null,
            RolledBackWorldState: rolledBackWorld,
            NowCurrentAt: rollBackTo?.CapturedAt,
            RemainingSaves: total - 1));
    }
}
