using BuildingBlocks.Contracts;
using Enemy.Service.DTOs;

namespace Enemy.Service.Services.Interface;

public interface IGameDataImportService
{
    Task<ApiResponse<GameDataImportResult>> ImportAsync(GameDataImportRequest request);
}