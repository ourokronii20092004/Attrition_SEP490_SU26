using BuildingBlocks.Contracts;
using Character.Service.DTOs;
using Character.Service.Models;
using Character.Service.Repositories;

namespace Character.Service.Services;

public class SessionService : ISessionService
{
    private readonly ISessionRepository _repo;
    private readonly ILogger<SessionService> _logger;

    public SessionService(ISessionRepository repo, ILogger<SessionService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<List<SessionSummaryDto>> GetByOwnerAsync(Guid ownerId)
    {
        var sessions = await _repo.GetByOwnerAsync(ownerId);
        return sessions.Select(ToSummary).ToList();
    }

    public async Task<SessionDetailDto?> GetDetailAsync(Guid sessionId)
    {
        var session = await _repo.GetDetailAsync(sessionId);
        if (session == null) return null;
        // Join character names so a room view can show who played instead of raw GUIDs.
        var names = await _repo.GetCharacterNamesAsync(session.Characters.Select(c => c.CharacterId).ToList());
        return ToDetail(session, names);
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
        entity.Combat.PotionMaxManaFlasks = request.PotionMaxManaFlasks;
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

    /// <summary>
    /// Consolidated save: the whole party in one request, one transaction.
    ///
    /// SECURITY: the host is authoritative for the ROOM (ownership is checked in the controller),
    /// but not for arbitrary characters. Each entry's OwnerId is verified against the characters
    /// table; a character that doesn't exist or belongs to someone else is SKIPPED and reported
    /// back rather than failing the whole save — one bad entry must not cost the other player
    /// their progress.
    /// </summary>
    public async Task<ApiResponse<BulkSaveResultDto>> BulkSaveAsync(BulkSaveRequest request)
    {
        if (request.SessionId == Guid.Empty)
            return ApiResponse<BulkSaveResultDto>.Fail("SessionId is required.");

        // Guard against a malformed payload naming the same row twice: dedupe (last entry wins) and
        // iterate the DEDUPED lists below. Iterating the raw list would Add a second entity with the
        // same primary key — a lookup only sees rows already in the graph, not ones added this pass —
        // and SaveChanges would throw, losing the whole save rather than just the duplicate.
        var characters = (request.Characters ?? new List<BulkCharacterDto>())
            .Where(c => c.CharacterId != Guid.Empty)
            .GroupBy(c => c.CharacterId)
            .Select(g => g.Last())
            .ToList();
        var worldStates = (request.WorldStates ?? new List<BulkWorldStateDto>())
            .Where(w => !string.IsNullOrWhiteSpace(w.EventId))
            .GroupBy(w => w.EventId, StringComparer.Ordinal)
            .Select(g => g.Last())
            .ToList();

        var characterIds = characters.Select(c => c.CharacterId).ToList();
        var eventIds = worldStates.Select(w => w.EventId).ToList();

        var graph = await _repo.LoadForBulkAsync(request.SessionId, characterIds, eventIds);
        if (graph.Session == null) return ApiResponse<BulkSaveResultDto>.Fail("Room not found.");

        var now = DateTime.UtcNow;
        var skipped = new List<Guid>();
        var saved = 0;
        var savedCharacterIds = new List<Guid>();

        foreach (var dto in characters)
        {
            // Ownership check: the character must exist AND belong to the claimed owner.
            var character = graph.Characters.FirstOrDefault(c => c.Id == dto.CharacterId);
            if (character == null || character.OwnerId != dto.OwnerId)
            {
                skipped.Add(dto.CharacterId);
                continue;
            }

            var entity = graph.CharacterSessions.FirstOrDefault(cs => cs.CharacterId == dto.CharacterId);
            if (entity == null)
            {
                entity = new CharacterSessionEntity
                {
                    CharacterId = dto.CharacterId,
                    SessionId = request.SessionId,
                    CreatedAt = now
                };
                _repo.AddCharacterSession(entity);
            }

            entity.PlayerRole = dto.PlayerRole;
            entity.CurrentLevel = dto.CurrentLevel;
            entity.CurrentExp = dto.CurrentExp;
            entity.DeathCount = dto.DeathCount;
            entity.AllocatedPointsJson = dto.AllocatedPointsJson ?? entity.AllocatedPointsJson;
            entity.Vitals.MaxHp = dto.MaxHp;
            entity.Vitals.CurrentHp = dto.CurrentHp;
            entity.Vitals.MaxMana = dto.MaxMana;
            entity.Vitals.CurrentMana = dto.CurrentMana;
            entity.Vitals.MaxStamina = dto.MaxStamina;
            entity.Combat.PotionMaxFlasks = dto.PotionMaxFlasks;
            entity.Combat.PotionMaxManaFlasks = dto.PotionMaxManaFlasks;
            entity.Combat.HealthCharges = dto.HealthCharges;
            entity.Combat.ManaCharges = dto.ManaCharges;
            entity.Combat.AttackSpeed = dto.AttackSpeed;
            entity.Combat.Ad = dto.Ad;
            entity.Combat.Ap = dto.Ap;
            entity.Combat.Def = dto.Def;
            entity.Combat.Res = dto.Res;
            entity.Position.PosX = dto.PosX;
            entity.Position.PosY = dto.PosY;
            entity.Position.PosZ = dto.PosZ;
            entity.Position.LastRestPointId = dto.LastRestPointId ?? entity.Position.LastRestPointId;
            // JSON blob null = keep existing (don't wipe inventory when a save omits it).
            entity.InventoryJson = dto.InventoryJson ?? entity.InventoryJson;
            entity.EquipmentJson = dto.EquipmentJson ?? entity.EquipmentJson;
            entity.UpdatedAt = now;

            // Mirror onto the global character row + append snapshot history. This is what makes the
            // CLIENT's progress visible on the web: the old snapshot path used the host's JWT for
            // OwnerId, so only the host's character ever got a row.
            character.InventoryJson = dto.InventoryJson ?? character.InventoryJson;
            character.EquipmentJson = dto.EquipmentJson ?? character.EquipmentJson;
            character.UpdatedAt = now;
            character.Snapshots.Add(new CharacterSnapshot
            {
                Level = dto.CurrentLevel,
                Hp = dto.CurrentHp,
                MaxHp = dto.MaxHp,
                Gold = 0,
                IsAlive = dto.IsAlive,
                RoomCode = string.IsNullOrWhiteSpace(request.RoomCode) ? graph.Session.RoomCode : request.RoomCode,
                EventType = string.IsNullOrWhiteSpace(request.EventType) ? "save" : request.EventType,
                PlaytimeSeconds = request.PlayTimeSeconds,
                CapturedAt = now
            });

            // Full save file — the complete state at this moment, which the web renders and which a
            // delete-newest can roll live state back to. The thin snapshot above is kept because
            // shipped game builds and the existing timeline UI still read it.
            _repo.AddCharacterSave(new CharacterSaveEntity
            {
                CharacterId = dto.CharacterId,
                SessionId = request.SessionId,
                RoomCode = string.IsNullOrWhiteSpace(request.RoomCode) ? graph.Session.RoomCode : request.RoomCode,
                CurrentScene = string.IsNullOrWhiteSpace(request.CurrentScene) ? graph.Session.CurrentScene : request.CurrentScene,
                EventType = string.IsNullOrWhiteSpace(request.EventType) ? "rest" : request.EventType,
                PlayerRole = dto.PlayerRole,
                CurrentLevel = dto.CurrentLevel,
                CurrentExp = dto.CurrentExp,
                DeathCount = dto.DeathCount,
                PlaytimeSeconds = request.PlayTimeSeconds,
                IsAlive = dto.IsAlive,
                AllocatedPointsJson = dto.AllocatedPointsJson,
                Vitals = new VitalStats
                {
                    MaxHp = dto.MaxHp, CurrentHp = dto.CurrentHp,
                    MaxMana = dto.MaxMana, CurrentMana = dto.CurrentMana,
                    MaxStamina = dto.MaxStamina,
                },
                Combat = new CombatStats
                {
                    AttackSpeed = dto.AttackSpeed,
                    PotionMaxFlasks = dto.PotionMaxFlasks, PotionMaxManaFlasks = dto.PotionMaxManaFlasks,
                    HealthCharges = dto.HealthCharges, ManaCharges = dto.ManaCharges,
                    Ad = dto.Ad, Ap = dto.Ap, Def = dto.Def, Res = dto.Res,
                },
                Position = new Position
                {
                    PosX = dto.PosX, PosY = dto.PosY, PosZ = dto.PosZ,
                    LastRestPointId = dto.LastRestPointId,
                },
                InventoryJson = dto.InventoryJson,
                EquipmentJson = dto.EquipmentJson,
                CapturedAt = now,
            });
            savedCharacterIds.Add(dto.CharacterId);

            saved++;
        }

        foreach (var dto in worldStates)
        {
            var entity = graph.WorldStates.FirstOrDefault(w => w.EventId == dto.EventId);
            if (entity == null)
            {
                entity = new WorldStateEntity { SessionId = request.SessionId, EventId = dto.EventId };
                _repo.AddWorldState(entity);
            }
            entity.StateValue = dto.StateValue;
            entity.Progress = dto.Progress;
            entity.UpdatedAt = now;
        }

        graph.Session.PlayTimeSeconds = request.PlayTimeSeconds;
        if (!string.IsNullOrWhiteSpace(request.CurrentScene)) graph.Session.CurrentScene = request.CurrentScene;
        if (request.FogJson != null) graph.Session.FogJson = request.FogJson;
        graph.Session.UpdatedAt = now;
        graph.Session.LastPlayedAt = now;

        // Room-state snapshot, captured AFTER the world-state and fog writes above so it reflects
        // the room as this save left it. Shares `now` with the character saves from the same push,
        // which is what lets a character save be paired with the room state around it.
        //
        // Only written when the save actually recorded something: a push that saved no characters
        // (all ownership-rejected) has no accompanying moment to snapshot.
        if (saved > 0)
        {
            var allWorldStates = graph.WorldStates
                .Select(w => new { eventId = w.EventId, stateValue = w.StateValue, progress = w.Progress })
                .ToList();
            _repo.AddRoomStateSave(new RoomStateSaveEntity
            {
                SessionId = request.SessionId,
                CapturedAt = now,
                EventType = string.IsNullOrWhiteSpace(request.EventType) ? "rest" : request.EventType,
                CurrentScene = graph.Session.CurrentScene,
                WorldStatesJson = allWorldStates.Count == 0
                    ? null
                    : System.Text.Json.JsonSerializer.Serialize(allWorldStates),
                FogJson = graph.Session.FogJson,
                PlayTimeSeconds = request.PlayTimeSeconds,
            });
        }

        // Single commit — everything above lands together or not at all.
        await _repo.SaveChangesAsync();

        // Retention: keep the newest N saves per character. Runs after the commit above because the
        // rows only get their ids (and their place in the ordering) once written. A failure here
        // must not fail the save itself — the player's progress is already safely stored, and an
        // over-long history is a housekeeping problem, not a data-loss one.
        foreach (var characterId in savedCharacterIds.Distinct())
        {
            try
            {
                var stale = await _repo.GetSaveIdsBeyondCapAsync(characterId, SaveRetention.MaxPerCharacter);
                if (stale.Count > 0)
                {
                    _repo.RemoveCharacterSaves(stale);
                    await _repo.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Save retention prune failed for character {CharacterId}", characterId);
            }
        }

        // Same cap for the room's own history, so a long-running room doesn't accumulate snapshots
        // without bound either.
        if (saved > 0)
        {
            try
            {
                var staleRooms = await _repo.GetRoomStateIdsBeyondCapAsync(request.SessionId, SaveRetention.MaxPerCharacter);
                if (staleRooms.Count > 0)
                {
                    _repo.RemoveRoomStateSaves(staleRooms);
                    await _repo.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Room-state retention prune failed for session {SessionId}", request.SessionId);
            }
        }

        return ApiResponse<BulkSaveResultDto>.Ok(
            new BulkSaveResultDto(request.SessionId, saved, eventIds.Count, skipped));
    }

    public async Task<ApiResponse> DeleteSessionAsync(Guid sessionId)    {
        if (sessionId == Guid.Empty)
            return ApiResponse.Fail("SessionId is required.");

        var deleted = await _repo.DeleteSessionAsync(sessionId);
        if (!deleted) return ApiResponse.Fail("Room not found.");

        return ApiResponse.Ok();
    }

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

    private static SessionSummaryDto ToSummary(SessionEntity s) => new(
        s.Id, s.OwnerId, s.RoomCode, s.Name, s.IsMultiplayer,
        s.PlayTimeSeconds, s.CurrentScene, s.CreatedAt, s.UpdatedAt, s.LastPlayedAt,
        s.Characters.Count);

    private static SessionDetailDto ToDetail(
        SessionEntity s,
        Dictionary<Guid, (string Name, string Archetype)>? names = null) => new(
        s.Id, s.OwnerId, s.RoomCode, s.Name, s.IsMultiplayer,
        s.PlayTimeSeconds, s.CurrentScene, s.CreatedAt, s.UpdatedAt, s.LastPlayedAt,
        s.Characters.Select(c => ToCharacterSessionDto(c, names)).ToList(),
        s.WorldStates.Select(ToWorldStateDto).ToList(),
        s.FogJson);

    private static CharacterSessionDto ToCharacterSessionDto(
        CharacterSessionEntity c,
        Dictionary<Guid, (string Name, string Archetype)>? names = null)
    {
        string? name = null, archetype = null;
        if (names != null && names.TryGetValue(c.CharacterId, out var meta))
            (name, archetype) = (meta.Name, meta.Archetype);

        return new(
            c.CharacterId, c.SessionId, c.PlayerRole, c.CurrentLevel, c.CurrentExp, c.AllocatedPointsJson,
            c.Vitals.MaxHp, c.Vitals.CurrentHp, c.Vitals.MaxMana, c.Vitals.CurrentMana, c.Vitals.MaxStamina,
            c.Combat.PotionMaxFlasks, c.Combat.PotionMaxManaFlasks, c.Combat.AttackSpeed, c.Position.PosX, c.Position.PosY,
            c.Position.LastRestPointId, c.InventoryJson, c.EquipmentJson, c.UpdatedAt,
            c.DeathCount, c.Combat.HealthCharges, c.Combat.ManaCharges, c.Position.PosZ,
            name, archetype,
            c.Combat.Ad, c.Combat.Ap, c.Combat.Def, c.Combat.Res);
    }

    private static WorldStateDto ToWorldStateDto(WorldStateEntity w) => new(
        w.EventId, w.StateValue, w.Progress, w.UpdatedAt);
}
