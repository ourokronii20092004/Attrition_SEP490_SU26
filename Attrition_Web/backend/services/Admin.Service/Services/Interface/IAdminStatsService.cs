using Admin.Service.DTOs;

namespace Admin.Service.Services.Interface;

public interface IAdminStatsService
{
    Task<AdminStatsDto> GetStatsAsync(CancellationToken ct);
}