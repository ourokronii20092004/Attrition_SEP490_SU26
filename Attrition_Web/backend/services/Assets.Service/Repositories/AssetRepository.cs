using Assets.Service.Data;
using Assets.Service.Models;
using BuildingBlocks.Persistence;

namespace Assets.Service.Repositories;

public class AssetRepository : Repository<Asset>, IAssetRepository
{
    public AssetRepository(AssetsDbContext context) : base(context) { }
}
