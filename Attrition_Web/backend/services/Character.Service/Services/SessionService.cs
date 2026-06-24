using BuildingBlocks.Contracts;
using Character.Service.DTOs;
using Character.Service.Models;
using Character.Service.Repositories;

namespace Character.Service.Services;

public class SessionService : ISessionService
{
    private readonly ISessionRepository _repo;

    public SessionService(ISessionRepository repo) => _repo = repo;

    public async Task<List<SessionSummaryDto>> GetByOwnerAsync(Guid ownerId)
    {
        var sessions = await _repo.GetByOwnerAsync(ownerId);
        return sessions.Select(ToSummary).ToList();
    }

    public async Task<SessionDetailDto?> GetDetailAsync(Guid sessionId)
    {
        var session = await _repo.GetDetailAsync(sessionId);
        return session == null ? null : ToDetail(session);
    }

    public async Task<SessionDetailDto?> GetByRoomCodeAsync(string roomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode)) return null;
        var session = await _repo.GetByRoomCodeAsync(roomCode.Trim().ToUpperInvariant());
        if (session == null) return null;
        // GetByRoomCode only includes Characters; pull the full detail for world state too.
        return await GetDetailAsync(session.Id);
    }

    public Task<Guid?> GetOwnerIdAsync(Guid sessionId) => _repo.GetOwnerIdAsync(sessionId);

    public async Task<ApiResponse<SessionDetailDto>> CreateOrReopenAsync(CreateSessionRequest request)
    {
        if (request.OwnerId == Guid.Empty)
            return ApiResponse<SessionDetailDto>.Fail("OwnerId is required.");
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiResponse<SessionDetailDto>.Fail("Room name is required.");

        // Re-open: a code was supplied and it already belongs to this host → return that room.
        // The client (Unity) generates the Fusion room code and sends it here, so the server's
        // fixed RoomCode MUST equal the code the two players are actually connected with. We honor
        // the supplied code on create (when free) instead of generating our own; only fall back to
        // server-side generation when the client sends none.
        string? requestedCode = string.IsNullOrWhiteSpace(request.RoomCode)
            ? null
            : request.RoomCode.Trim().ToUpperInvariant();

        if (requestedCode != null)
        {
            var existing = await _repo.GetByRoomCodeAsync(requestedCode);
            if (existing != null)
            {
                if (existing.OwnerId != request.OwnerId)
                    return ApiResponse<SessionDetailDto>.Fail("Room code already in use by another host.");
                existing.LastPlayedAt = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(request.CurrentScene)) existing.CurrentScene = request.CurrentScene;
                await _repo.UpdateAsync(existing);
                return ApiResponse<SessionDetailDto>.Ok((await GetDetailAsync(existing.Id))!);
            }
        }

        var session = new SessionEntity
        {
            OwnerId = request.OwnerId,
            Name = request.Name.Trim(),
            // Honor the client's Fusion code so server code == join code; else generate a unique one.
            RoomCode = requestedCode ?? await GenerateUniqueRoomCodeAsync(),
            IsMultiplayer = true,
            CurrentScene = request.CurrentScene,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastPlayedAt = DateTime.UtcNow
        };
        await _repo.AddAsync(session);
        return ApiResponse<SessionDetailDto>.Ok((await GetDetailAsync(session.Id))!);
    }

    public async Task<ApiResponse<SessionDetailDto>> UpdateMetaAsync(UpdateSessionRequest request)
    {
        var session = await _repo.GetDetailAsync(request.SessionId);
        if (session == null) return ApiResponse<SessionDetailDto>.Fail("Room not found.");

        session.PlayTimeSeconds = request.PlayTimeSeconds;
        if (!string.IsNullOrWhiteSpace(request.CurrentScene)) session.CurrentScene = request.CurrentScene;
        session.LastPlayedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(session);
        return ApiResponse<SessionDetailDto>.Ok(ToDetail(session));
    }

    public async Task<ApiResponse<CharacterSessionDto>> SaveCharacterSessionAsync(SaveCharacterSessionRequest request)
    {
        if (request.CharacterId == Guid.Empty || request.SessionId == Guid.Empty)
            return ApiResponse<CharacterSessionDto>.Fail("CharacterId and SessionId are required.");

        var existing = await _repo.GetCharacterSessionAsync(request.CharacterId, request.SessionId);
        var entity = existing ?? new CharacterSessionEntity
        {
            CharacterId = request.CharacterId,
            SessionId = request.SessionId,
            CreatedAt = DateTime.UtcNow
        };

        entity.PlayerRole = request.PlayerRole;
        entity.CurrentLevel = request.CurrentLevel;
        entity.CurrentExp = request.CurrentExp;
        entity.AllocatedPointsJson = request.AllocatedPointsJson ?? entity.AllocatedPointsJson;
        entity.Vitals.MaxHp = request.MaxHp;
        entity.Vitals.CurrentHp = request.CurrentHp;
        entity.Vitals.MaxMana = request.MaxMana;
        entity.Vitals.CurrentMana = request.CurrentMana;
        entity.Vitals.MaxStamina = request.MaxStamina;
        entity.Combat.PotionMaxFlasks = request.PotionMaxFlasks;
        entity.Combat.AttackSpeed = request.AttackSpeed;
        entity.Position.PosX = request.PosX;
        entity.Position.PosY = request.PosY;
        entity.Position.LastRestPointId = request.LastRestPointId ?? entity.Position.LastRestPointId;
        // JSON blob null = keep existing (don't wipe inventory when a save omits it).
        entity.InventoryJson = request.InventoryJson ?? entity.InventoryJson;
        entity.EquipmentJson = request.EquipmentJson ?? entity.EquipmentJson;
        entity.UpdatedAt = DateTime.UtcNow;

        await _repo.UpsertCharacterSessionAsync(entity);
        return ApiResponse<CharacterSessionDto>.Ok(ToCharacterSessionDto(entity));
    }

    public async Task<ApiResponse<WorldStateDto>> SaveWorldStateAsync(SaveWorldStateRequest request)
    {
        if (request.SessionId == Guid.Empty)
            return ApiResponse<WorldStateDto>.Fail("SessionId is required.");
        if (string.IsNullOrWhiteSpace(request.EventId))
            return ApiResponse<WorldStateDto>.Fail("EventId is required.");

        var existing = await _repo.GetWorldStateAsync(request.SessionId, request.EventId);
        var entity = existing ?? new WorldStateEntity
        {
            SessionId = request.SessionId,
            EventId = request.EventId
        };
        entity.StateValue = request.StateValue;
        entity.Progress = request.Progress;
        entity.UpdatedAt = DateTime.UtcNow;

        await _repo.UpsertWorldStateAsync(entity);
        return ApiResponse<WorldStateDto>.Ok(ToWorldStateDto(entity));
    }

    public async Task<ApiResponse> DeleteSessionAsync(Guid sessionId)
    {
        if (sessionId == Guid.Empty)
            return ApiResponse.Fail("SessionId is required.");

        var deleted = await _repo.DeleteSessionAsync(sessionId);
        if (!deleted) return ApiResponse.Fail("Room not found.");

        return ApiResponse.Ok();
    }

    // ─── room code ───
    // Fixed per room: generated once at creation and never changes, so a host can re-open the
    // same journey and invite the same friend back with the same code.
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no O/0/I/1 to avoid confusion
    private static readonly Random Rng = new();

    private async Task<string> GenerateUniqueRoomCodeAsync()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = string.Create(6, 0, (span, _) =>
            {
                for (var i = 0; i < span.Length; i++)
                    span[i] = CodeAlphabet[Rng.Next(CodeAlphabet.Length)];
            });
            if (await _repo.RoomCodeIsFreeAsync(code)) return code;
        }
        // Extremely unlikely; widen to 8 chars as a fallback.
        return string.Create(8, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = CodeAlphabet[Rng.Next(CodeAlphabet.Length)];
        });
    }

    // ─── mapping ───
    private static SessionSummaryDto ToSummary(SessionEntity s) => new(
        s.Id, s.OwnerId, s.RoomCode, s.Name, s.IsMultiplayer,
        s.PlayTimeSeconds, s.CurrentScene, s.CreatedAt, s.UpdatedAt, s.LastPlayedAt,
        s.Characters.Count);

    private static SessionDetailDto ToDetail(SessionEntity s) => new(
        s.Id, s.OwnerId, s.RoomCode, s.Name, s.IsMultiplayer,
        s.PlayTimeSeconds, s.CurrentScene, s.CreatedAt, s.UpdatedAt, s.LastPlayedAt,
        s.Characters.Select(ToCharacterSessionDto).ToList(),
        s.WorldStates.Select(ToWorldStateDto).ToList());

    private static CharacterSessionDto ToCharacterSessionDto(CharacterSessionEntity c) => new(
        c.CharacterId, c.SessionId, c.PlayerRole, c.CurrentLevel, c.CurrentExp, c.AllocatedPointsJson,
        c.Vitals.MaxHp, c.Vitals.CurrentHp, c.Vitals.MaxMana, c.Vitals.CurrentMana, c.Vitals.MaxStamina,
        c.Combat.PotionMaxFlasks, c.Combat.AttackSpeed, c.Position.PosX, c.Position.PosY,
        c.Position.LastRestPointId, c.InventoryJson, c.EquipmentJson, c.UpdatedAt);

    private static WorldStateDto ToWorldStateDto(WorldStateEntity w) => new(
        w.EventId, w.StateValue, w.Progress, w.UpdatedAt);
}
