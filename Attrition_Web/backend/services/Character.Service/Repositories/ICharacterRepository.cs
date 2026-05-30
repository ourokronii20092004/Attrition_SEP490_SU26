using BuildingBlocks.Persistence;
using Character.Service.Models;

namespace Character.Service.Repositories;

public interface ICharacterRepository : IRepository<CharacterEntity>
{
    Task<CharacterEntity?> GetWithSnapshotsAsync(Guid id);
    Task<List<CharacterEntity>> GetByOwnerWithSnapshotsAsync(Guid ownerId);
    Task<List<CharacterEntity>> GetAllWithSnapshotsAsync();
    Task<CharacterEntity?> FindByOwnerAndNameAsync(Guid ownerId, string name);
}
