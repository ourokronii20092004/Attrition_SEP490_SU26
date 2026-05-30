using BuildingBlocks.Persistence;
using Character.Service.Data;
using Character.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Character.Service.Repositories;

public class CharacterRepository : Repository<CharacterEntity>, ICharacterRepository
{
    private readonly CharacterDbContext _context;

    public CharacterRepository(CharacterDbContext context) : base(context) => _context = context;

    public async Task<CharacterEntity?> GetWithSnapshotsAsync(Guid id) =>
        await _context.Characters
            .Include(c => c.Snapshots)
            .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<List<CharacterEntity>> GetByOwnerWithSnapshotsAsync(Guid ownerId) =>
        await _context.Characters
            .Include(c => c.Snapshots)
            .Where(c => c.OwnerId == ownerId)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync();

    public async Task<List<CharacterEntity>> GetAllWithSnapshotsAsync() =>
        await _context.Characters
            .Include(c => c.Snapshots)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync();

    // Case-sensitive match, kept deliberately consistent with the case-sensitive unique index on
    // (OwnerId, Name). If these two ever disagree on what counts as a duplicate, the race-catch in
    // IngestSnapshotAsync can misfire. Change both together (e.g. to a functional lower(Name) index).
    public async Task<CharacterEntity?> FindByOwnerAndNameAsync(Guid ownerId, string name) =>
        await _context.Characters
            .Include(c => c.Snapshots)
            .FirstOrDefaultAsync(c => c.OwnerId == ownerId && c.Name == name);
}
