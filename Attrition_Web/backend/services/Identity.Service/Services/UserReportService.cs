using BuildingBlocks.Contracts;
using Identity.Service.DTOs;
using Identity.Service.Models;

namespace Identity.Service.Services;

public class UserReportService : IUserReportService
{
    private readonly IUserReportRepository _reports;
    private readonly IUserRepository _users;

    public UserReportService(IUserReportRepository reports, IUserRepository users)
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
            r.ReportedUserName ?? "Unknown", r.ReporterName ?? "Unknown", r.Reason, r.Status, r.CreatedAt,
            r.ActionTaken, r.ModeratorNote, r.ResolvedByName, r.ResolvedAt)).ToList();
        return new PaginatedResponse<AdminUserReportDto>(dtos, total, page, pageSize);
    }

    public async Task<ApiResponse> ResolveAsync(Guid reportId, bool banUser, string? note, string? adminName)
    {
        var report = await _reports.GetByIdAsync(reportId);
        if (report == null) return ApiResponse.Fail("Report not found.");

        var action = "None";
        if (banUser)
        {
            var reported = await _users.GetByIdAsync(report.ReportedUserId);
            if (reported != null && !reported.IsDeleted)
            {
                reported.IsBanned = true;
                reported.Refresh = new() { Token = null, ExpiresAt = null }; // kill active sessions
                await _users.UpdateAsync(reported);
                action = "Banned";
            }
        }

        report.Status = "Resolved";
        report.ActionTaken = action;
        report.ModeratorNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        report.ResolvedByName = adminName;
        report.ResolvedAt = DateTime.UtcNow;
        await _reports.UpdateAsync(report);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> DismissAsync(Guid reportId, string? adminName)
    {
        var report = await _reports.GetByIdAsync(reportId);
        if (report == null) return ApiResponse.Fail("Report not found.");
        report.Status = "Dismissed";
        report.ActionTaken = "None";
        report.ResolvedByName = adminName;
        report.ResolvedAt = DateTime.UtcNow;
        await _reports.UpdateAsync(report);
        return ApiResponse.Ok();
    }
}