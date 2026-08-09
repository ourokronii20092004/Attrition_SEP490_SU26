using BuildingBlocks.Persistence;
using Character.Service.Models;

namespace Character.Service.Repositories.Interface;

public interface ICharacterRepository : IRepository<CharacterEntity>
{
    Task<CharacterEntity?> GetWithSnapshotsAsync(Guid id);

    // Owner + name only, for the ownership guard on save endpoints. Loading the whole snapshot
    // graph just to compare one guid would be wasteful on every save request.
    Task<(Guid OwnerId, string Name)?> GetOwnerAndNameAsync(Guid id);

    Task<List<CharacterEntity>> GetByOwnerWithSnapshotsAsync(Guid ownerId);

    Task<(List<CharacterEntity> Items, int TotalCount)> GetPagedWithSnapshotsAsync(int page, int pageSize);

    Task<CharacterEntity?> FindByOwnerAndNameAsync(Guid ownerId, string name);
}