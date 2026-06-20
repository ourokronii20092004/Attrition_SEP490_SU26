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

    // ─── character_session upsert/read ───

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

    // ─── world_state upsert ───

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
}
