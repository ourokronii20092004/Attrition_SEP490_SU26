using Assets.Service.Data;
using Assets.Service.Models;
using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Assets.Service.Repositories;

public class AssetRepository : Repository<Asset>, IAssetRepository
{
    private readonly AssetsDbContext _context;

    public AssetRepository(AssetsDbContext context) : base(context) => _context = context;

    public Task<Asset?> GetBySourceAsync(string sourceType, string sourceId) =>
        _context.Assets.FirstOrDefaultAsync(x => x.SourceType == sourceType && x.SourceId == sourceId);

    public async Task AddTrackedAsync(Asset asset) => await _context.Assets.AddAsync(asset);

    public void Detach(Asset asset) => _context.Entry(asset).State = EntityState.Detached;

    public Task SaveAsync() => _context.SaveChangesAsync();
}