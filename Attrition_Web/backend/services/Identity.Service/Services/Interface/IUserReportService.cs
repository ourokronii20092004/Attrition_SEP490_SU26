using BuildingBlocks.Contracts;
using Identity.Service.DTOs;

namespace Identity.Service.Services.Interface;

public interface IUserReportService
{
    Task<ApiResponse> ReportUserAsync(Guid reportedUserId, string reason, Guid reporterId, string? reporterName);
    Task<PaginatedResponse<AdminUserReportDto>> ListReportsAsync(string status, int page, int pageSize);
    Task<ApiResponse> ResolveAsync(Guid reportId, bool banUser, string? note, string? adminName);
    Task<ApiResponse> DismissAsync(Guid reportId, string? adminName);
}
