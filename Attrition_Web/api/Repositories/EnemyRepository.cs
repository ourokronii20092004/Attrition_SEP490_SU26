using Attrition.API.Data;
using Attrition.API.Models;

namespace Attrition.API.Repositories;

public class EnemyRepository : Repository<Enemy>, IEnemyRepository
{
    public EnemyRepository(AppDbContext db) : base(db)
    {
    }
}
