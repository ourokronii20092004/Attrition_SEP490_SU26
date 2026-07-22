using Enemy.Service.DTOs;

namespace Enemy.Service.Repositories.Interface;

public interface IGameDataImportRepository
{
    Task<GameDataImportResult> ImportAsync(GameDataImportRequest request);
}
