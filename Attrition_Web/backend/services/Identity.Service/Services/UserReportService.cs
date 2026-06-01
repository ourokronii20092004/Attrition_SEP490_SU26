using BuildingBlocks.Contracts;
using BuildingBlocks.Persistence;
using Identity.Service.DTOs;
using Identity.Service.Models;
using Identity.Service.Repositories;

namespace Identity.Service.Services;

public interface IUserReportService
{
    Task<ApiResponse> ReportUserAsync(Guid reportedUserId, string reason, Guid reporterId, string? reporterName);
    Task<PaginatedResponse<AdminUserReportDto>> ListReportsAsync(string status, int page, int pageSize);
    Task<ApiResponse> ResolveAsync(Guid reportId);
    Task<ApiResponse> DismissAsync(Guid reportId);
}

public class UserReportService : IUserReportService
{
    private readonly IRepository<UserReport> _reports;
    private readonly IUserRepository _users;

    public UserReportService(IRepository<UserReport> reports, IUserRepository users)
    {
        _reports = reports;
        _users = users;
    }

    public async Task<ApiResponse> ReportUserAsync(Guid reportedUserId, string reason, Guid reporterId, string? reporterName)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return ApiResponse.Fail("A reason is required.");
        if (reportedUserId == reporterId)
            return ApiResponse.Fail("You can't report yourself.");

        var reported = await _users.GetByIdAsync(reportedUserId);
        if (reported == null || reported.IsDeleted)
            return ApiResponse.Fail("User not found.");

        await _reports.AddAsync(new UserReport
        {
            ReportedUserId = reportedUserId,
            ReportedUserName = reported.Username,
            ReporterId = reporterId,
            ReporterName = reporterName,
            Reason = reason.Trim(),
            Status = "Pending"
        });
        return new ApiResponse(true, "Report submitted. Thank you.");
    }

    public async Task<PaginatedResponse<AdminUserReportDto>> ListReportsAsync(string status, int page, int pageSize)
    {
        var (items, total) = await _reports.GetPagedAsync(page, pageSize,
            r => r.Status == status, q => q.OrderByDescending(r => r.CreatedAt));
        var dtos = items.Select(r => new AdminUserReportDto(r.Id, r.ReportedUserId,
            r.ReportedUserName ?? "Unknown", r.ReporterName ?? "Unknown", r.Reason, r.Status, r.CreatedAt)).ToList();
        return new PaginatedResponse<AdminUserReportDto>(dtos, total, page, pageSize);
    }

    public async Task<ApiResponse> ResolveAsync(Guid reportId)
    {
        var report = await _reports.GetByIdAsync(reportId);
        if (report == null) return ApiResponse.Fail("Report not found.");
        report.Status = "Resolved";
        await _reports.UpdateAsync(report);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> DismissAsync(Guid reportId)
    {
        var report = await _reports.GetByIdAsync(reportId);
        if (report == null) return ApiResponse.Fail("Report not found.");
        report.Status = "Dismissed";
        await _reports.UpdateAsync(report);
        return ApiResponse.Ok();
    }
}
