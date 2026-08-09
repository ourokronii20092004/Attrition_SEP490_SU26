using Character.Service.Data;
using Character.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Character.Service.Repositories;

public class SessionRepository : ISessionRepository
{
    private readonly CharacterDbContext _context;

    public SessionRepository(CharacterDbContext context) => _context = context;

    public async Task<List<SessionEntity>> GetByOwnerAsync(Guid ownerId) =>
        await _context.Sessions
            .Include(s => s.Characters)
            .Where(s => s.OwnerId == ownerId)
            .OrderByDescending(s => s.LastPlayedAt)
            .ToListAsync();

    public async Task<SessionEntity?> GetDetailAsync(Guid sessionId) =>
        await _context.Sessions
            .Include(s => s.Characters)
            .Include(s => s.WorldStates)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

    public async Task<SessionEntity?> GetByRoomCodeAsync(string roomCode) =>
        await _context.Sessions
            .Include(s => s.Characters)
            .FirstOrDefaultAsync(s => s.RoomCode == roomCode);

    public async Task<Guid?> GetOwnerIdAsync(Guid sessionId) =>
        await _context.Sessions
            .Where(s => s.Id == sessionId)
            .Select(s => (Guid?)s.OwnerId)
            .FirstOrDefaultAsync();

    public async Task<Dictionary<Guid, (string Name, string Archetype)>> GetCharacterNamesAsync(List<Guid> characterIds)
    {
        if (characterIds.Count == 0) return new();
        var rows = await _context.Characters
            .AsNoTracking()
            .Where(c => characterIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.Archetype })
            .ToListAsync();
        return rows.ToDictionary(r => r.Id, r => (r.Name, r.Archetype));
    }

    public async Task<SessionEntity> AddAsync(SessionEntity session)
    {
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task UpdateAsync(SessionEntity session)
    {
        session.UpdatedAt = DateTime.UtcNow;
        _context.Sessions.Update(session);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> RoomCodeIsFreeAsync(string roomCode) =>
        !await _context.Sessions.AnyAsync(s => s.RoomCode == roomCode);

    public async Task<CharacterSessionEntity?> GetCharacterSessionAsync(Guid characterId, Guid sessionId) =>
        await _context.CharacterSessions
            .FirstOrDefaultAsync(cs => cs.CharacterId == characterId && cs.SessionId == sessionId);

    public async Task UpsertCharacterSessionAsync(CharacterSessionEntity entity)
    {
        var existing = await _context.CharacterSessions
            .FirstOrDefaultAsync(cs => cs.CharacterId == entity.CharacterId && cs.SessionId == entity.SessionId);
        if (existing == null)
            _context.CharacterSessions.Add(entity);
        else
            _context.Entry(existing).CurrentValues.SetValues(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<WorldStateEntity?> GetWorldStateAsync(Guid sessionId, string eventId) =>
        await _context.WorldStates
            .FirstOrDefaultAsync(w => w.SessionId == sessionId && w.EventId == eventId);

    public async Task UpsertWorldStateAsync(WorldStateEntity entity)
    {
        var existing = await _context.WorldStates
            .FirstOrDefaultAsync(w => w.SessionId == entity.SessionId && w.EventId == entity.EventId);
        if (existing == null)
            _context.WorldStates.Add(entity);
        else
            _context.Entry(existing).CurrentValues.SetValues(entity);
        await _context.SaveChangesAsync();
    }

    // ── Consolidated bulk save ───────────────────────────────────────────────────────────────
    // Load everything one push touches in 4 queries, TRACKED (no AsNoTracking) so the service can
    // mutate the graph in place. Characters are fetched by id AND by owner-scoped snapshot need:
    // the global rows carry snapshot history and are what we check ownership against.
    public async Task<BulkSaveGraph> LoadForBulkAsync(Guid sessionId, List<Guid> characterIds, List<string> eventIds)
    {
        var session = await _context.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);

        var characterSessions = characterIds.Count == 0
            ? new List<CharacterSessionEntity>()
            : await _context.CharacterSessions
                .Where(cs => cs.SessionId == sessionId && characterIds.Contains(cs.CharacterId))
                .ToListAsync();

        var worldStates = eventIds.Count == 0
            ? new List<WorldStateEntity>()
            : await _context.WorldStates
                .Where(w => w.SessionId == sessionId && eventIds.Contains(w.EventId))
                .ToListAsync();

        // Snapshots is an owned collection — Include it so appending a snapshot doesn't wipe the
        // existing timeline when EF materializes the parent without its children.
        var characters = characterIds.Count == 0
            ? new List<CharacterEntity>()
            : await _context.Characters
                .Include(c => c.Snapshots)
                .Where(c => characterIds.Contains(c.Id))
                .ToListAsync();

        return new BulkSaveGraph(session, characterSessions, worldStates, characters);
    }

    public void AddCharacterSession(CharacterSessionEntity entity) => _context.CharacterSessions.Add(entity);

    public void AddWorldState(WorldStateEntity entity) => _context.WorldStates.Add(entity);

    // One commit for the whole party — a partial save would leave two players inconsistent.
    public Task SaveChangesAsync() => _context.SaveChangesAsync();

    // Delete a room entirely: session row + all character progress + world state.
    public async Task<bool> DeleteSessionAsync(Guid sessionId)
    {
        var session = await _context.Sessions
            .Include(s => s.Characters)
            .Include(s => s.WorldStates)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session == null) return false;

        if (session.Characters.Count > 0) _context.CharacterSessions.RemoveRange(session.Characters);
        if (session.WorldStates.Count > 0) _context.WorldStates.RemoveRange(session.WorldStates);
        _context.Sessions.Remove(session);
        await _context.SaveChangesAsync();
        return true;
    }

    // ── Save files ───────────────────────────────────────────────────────────────────────────

    public void AddCharacterSave(CharacterSaveEntity entity) => _context.CharacterSaves.Add(entity);

    public async Task<List<long>> GetSaveIdsBeyondCapAsync(Guid characterId, int keep) =>
        await _context.CharacterSaves
            .Where(x => x.CharacterId == characterId)
            .OrderByDescending(x => x.CapturedAt).ThenByDescending(x => x.Id)
            .Skip(keep)
            .Select(x => x.Id)
            .ToListAsync();

    public void RemoveCharacterSaves(List<long> ids)
    {
        if (ids.Count == 0) return;
        // Stubs rather than a load: only the key matters for a delete.
        foreach (var id in ids)
            _context.CharacterSaves.Remove(new CharacterSaveEntity { Id = id });
    }

    public async Task<(List<CharacterSaveEntity> Items, int Total)> GetSavesAsync(Guid characterId, int page, int pageSize)
    {
        var query = _context.CharacterSaves.AsNoTracking().Where(x => x.CharacterId == characterId);
        var total = await query.CountAsync();
        // Id breaks ties: two saves can share a CapturedAt at second resolution, and an unstable
        // order would make "newest" ambiguous — which decides whether a delete rolls back.
        var items = await query
            .OrderByDescending(x => x.CapturedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();
        return (items, total);
    }

    public async Task<CharacterSaveEntity?> GetSaveAsync(long saveId) =>
        await _context.CharacterSaves.AsNoTracking().FirstOrDefaultAsync(x => x.Id == saveId);

    public async Task<int> CountSavesAsync(Guid characterId) =>
        await _context.CharacterSaves.CountAsync(x => x.CharacterId == characterId);

    public async Task<CharacterSaveEntity?> GetNewestSaveExcludingAsync(Guid characterId, long excludeSaveId) =>
        await _context.CharacterSaves.AsNoTracking()
            .Where(x => x.CharacterId == characterId && x.Id != excludeSaveId)
            .OrderByDescending(x => x.CapturedAt).ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync();

    /// <summary>
    /// Delete a save and, when <paramref name="rollBackTo"/> is given, rewrite the live
    /// character_session row from it — both inside one transaction.
    ///
    /// Only fields the game actually consumes on spawn are restored. Per SAVE_PAYLOAD_FORMAT.md,
    /// Ad/Ap/Def/Res are display-only: PlayerStats recomputes them from the base sheet plus gear,
    /// and never reads the stored values back. Writing them into live state would put numbers there
    /// that the game will immediately contradict, so they are deliberately left alone.
    /// </summary>
    public async Task<bool> DeleteSaveAndRollBackAsync(CharacterSaveEntity save, CharacterSaveEntity? rollBackTo)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        var ok = false;
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync();

            var rows = await _context.CharacterSaves.Where(x => x.Id == save.Id).ExecuteDeleteAsync();
            if (rows == 0) { await tx.RollbackAsync(); ok = false; return; }

            // Roll back live state only when the deleted save was the newest AND it belonged to a
            // room. A save taken outside a room (character creation) has no live row to restore.
            if (rollBackTo != null && save.SessionId is { } sessionId)
            {
                var live = await _context.CharacterSessions
                    .FirstOrDefaultAsync(x => x.CharacterId == save.CharacterId && x.SessionId == sessionId);
                if (live != null)
                {
                    live.CurrentLevel = rollBackTo.CurrentLevel;
                    live.CurrentExp = rollBackTo.CurrentExp;
                    live.DeathCount = rollBackTo.DeathCount;
                    live.AllocatedPointsJson = rollBackTo.AllocatedPointsJson;

                    live.Vitals.MaxHp = rollBackTo.Vitals.MaxHp;
                    live.Vitals.CurrentHp = rollBackTo.Vitals.CurrentHp;
                    live.Vitals.MaxMana = rollBackTo.Vitals.MaxMana;
                    live.Vitals.CurrentMana = rollBackTo.Vitals.CurrentMana;
                    live.Vitals.MaxStamina = rollBackTo.Vitals.MaxStamina;

                    // Flask capacity and remaining charges ARE read back on spawn.
                    live.Combat.PotionMaxFlasks = rollBackTo.Combat.PotionMaxFlasks;
                    live.Combat.PotionMaxManaFlasks = rollBackTo.Combat.PotionMaxManaFlasks;
                    live.Combat.HealthCharges = rollBackTo.Combat.HealthCharges;
                    live.Combat.ManaCharges = rollBackTo.Combat.ManaCharges;
                    live.Combat.AttackSpeed = rollBackTo.Combat.AttackSpeed;
                    // Ad/Ap/Def/Res intentionally not restored - see the summary above.

                    live.Position.PosX = rollBackTo.Position.PosX;
                    live.Position.PosY = rollBackTo.Position.PosY;
                    live.Position.PosZ = rollBackTo.Position.PosZ;
                    live.Position.LastRestPointId = rollBackTo.Position.LastRestPointId;

                    live.InventoryJson = rollBackTo.InventoryJson;
                    live.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                }
            }

            await tx.CommitAsync();
            ok = true;
        });
        return ok;
    }

    // ── Room-state snapshots ─────────────────────────────────────────────────────────────────

    public void AddRoomStateSave(RoomStateSaveEntity entity) => _context.RoomStateSaves.Add(entity);

    public async Task<List<long>> GetRoomStateIdsBeyondCapAsync(Guid sessionId, int keep) =>
        await _context.RoomStateSaves
            .Where(x => x.SessionId == sessionId)
            .OrderByDescending(x => x.CapturedAt).ThenByDescending(x => x.Id)
            .Skip(keep)
            .Select(x => x.Id)
            .ToListAsync();

    public void RemoveRoomStateSaves(List<long> ids)
    {
        if (ids.Count == 0) return;
        foreach (var id in ids)
            _context.RoomStateSaves.Remove(new RoomStateSaveEntity { Id = id });
    }

    /// <summary>
    /// The room snapshot for a given moment. Matches at-or-before rather than exactly, because a
    /// bulk save that rejected every character writes no room snapshot — so the nearest earlier one
    /// is the correct thing to restore.
    /// </summary>
    public async Task<RoomStateSaveEntity?> GetRoomStateAtOrBeforeAsync(Guid sessionId, DateTime capturedAt) =>
        await _context.RoomStateSaves.AsNoTracking()
            .Where(x => x.SessionId == sessionId && x.CapturedAt <= capturedAt)
            .OrderByDescending(x => x.CapturedAt).ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync();

    /// <summary>
    /// Replace the room's world state and fog with a snapshot's.
    ///
    /// Deletes every existing world_state row for the room and re-inserts the snapshot's, rather
    /// than upserting: a boss defeated *after* the snapshot has no row in it, so a merge would leave
    /// that kill in place and the "rollback" would be a lie. Fog is overwritten wholesale for the
    /// same reason.
    ///
    /// Room-scoped by design — character rows are untouched, so one player's rollback of the shared
    /// world never rewrites another player's own progress.
    /// </summary>
    public async Task RestoreRoomStateAsync(Guid sessionId, RoomStateSaveEntity snapshot)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync();

            await _context.WorldStates.Where(w => w.SessionId == sessionId).ExecuteDeleteAsync();

            if (!string.IsNullOrWhiteSpace(snapshot.WorldStatesJson))
            {
                var rows = System.Text.Json.JsonSerializer.Deserialize<List<RestoredWorldState>>(
                    snapshot.WorldStatesJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                foreach (var r in rows ?? new List<RestoredWorldState>())
                {
                    if (string.IsNullOrWhiteSpace(r.EventId)) continue;
                    _context.WorldStates.Add(new WorldStateEntity
                    {
                        SessionId = sessionId,
                        EventId = r.EventId,
                        StateValue = r.StateValue,
                        Progress = r.Progress,
                        UpdatedAt = DateTime.UtcNow,
                    });
                }
            }

            var session = await _context.Sessions.FirstOrDefaultAsync(x => x.Id == sessionId);
            if (session != null)
            {
                session.FogJson = snapshot.FogJson;
                if (!string.IsNullOrWhiteSpace(snapshot.CurrentScene)) session.CurrentScene = snapshot.CurrentScene;
                session.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        });
    }

    public async Task<(List<SessionEntity> Items, int Total)> GetRoomsPagedAsync(int page, int pageSize)
    {
        var query = _context.Sessions.AsNoTracking();
        var total = await query.CountAsync();
        var items = await query
            .Include(s => s.Characters)
            .Include(s => s.WorldStates)
            .OrderByDescending(s => s.LastPlayedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();
        return (items, total);
    }

    public async Task<(int Rooms, int Multiplayer)> GetRoomStatsAsync()
    {
        // One round-trip for both numbers: the dashboard shows them together.
        var grouped = await _context.Sessions.AsNoTracking()
            .GroupBy(s => s.IsMultiplayer)
            .Select(g => new { IsMultiplayer = g.Key, Count = g.Count() })
            .ToListAsync();
        return (grouped.Sum(g => g.Count), grouped.Where(g => g.IsMultiplayer).Sum(g => g.Count));
    }

    public async Task<List<RoomStateSaveEntity>> GetRoomStateHistoryAsync(Guid sessionId, int limit) =>
        await _context.RoomStateSaves.AsNoTracking()
            .Where(x => x.SessionId == sessionId)
            .OrderByDescending(x => x.CapturedAt).ThenByDescending(x => x.Id)
            .Take(limit)
            .ToListAsync();

    /// <summary>Shape of one entry in RoomStateSaveEntity.WorldStatesJson.</summary>
    private sealed record RestoredWorldState(string EventId, short StateValue, int Progress);
}