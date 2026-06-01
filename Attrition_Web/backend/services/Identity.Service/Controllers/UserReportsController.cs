using BuildingBlocks.Authentication;
using BuildingBlocks.Contracts;
using Identity.Service.DTOs;
using Identity.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Service.Controllers;

// QOLF-9: a logged-in user reports another user; admins review via AdminUserReportsController.
[ApiController]
[Route("api/users")]
public class UserReportsController : ControllerBase
{
    private readonly IUserReportService _reports;
    private readonly ICurrentUser _user;
    public UserReportsController(IUserReportService reports, ICurrentUser user)
    {
        _reports = reports;
        _user = user;
    }

    [Authorize]
    [HttpPost("{id:guid}/report")]
    public async Task<IActionResult> Report(Guid id, [FromBody] ReportUserRequest request)
    {
        if (this.RequireUserId(_user, out var userId) is { } error) return error;
        var result = await _reports.ReportUserAsync(id, request.Reason, userId, _user.Username);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
