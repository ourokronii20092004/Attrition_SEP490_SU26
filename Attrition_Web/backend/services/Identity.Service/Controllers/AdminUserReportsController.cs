using BuildingBlocks.Contracts;
using Identity.Service.DTOs;
using Identity.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Service.Controllers;

// QOLF-9: admin moderation queue for user-against-user reports.
[ApiController]
[Route("api/admin/user-reports")]
[Authorize(Roles = Roles.Admin)]
public class AdminUserReportsController : ControllerBase
{
    private readonly IUserReportService _reports;
    public AdminUserReportsController(IUserReportService reports) => _reports = reports;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string status = "Pending",
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _reports.ListReportsAsync(status, page, pageSize);
        return Ok(ApiResponse<PaginatedResponse<AdminUserReportDto>>.Ok(result));
    }

    [HttpPut("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id)
    {
        var result = await _reports.ResolveAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPut("{id:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid id)
    {
        var result = await _reports.DismissAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
