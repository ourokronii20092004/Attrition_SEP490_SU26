using BuildingBlocks.Contracts;
using Identity.Service.DTOs;
using Identity.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Service.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = Roles.Admin)]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _admin;
    public AdminUsersController(IAdminUserService admin) => _admin = admin;

    // A "not found" failure should be 404, not 400. The service reports it via the error message.
    private IActionResult MapFailure(ApiResponse result) =>
        result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
            ? NotFound(result)
            : BadRequest(result);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? sort = null)
    {
        var result = await _admin.ListUsersAsync(page, pageSize, search, sort);
        return Ok(ApiResponse<PaginatedResponse<DTOs.UserListItem>>.Ok(result));
    }

    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> ChangeRole(Guid id, [FromBody] ChangeRoleRequest req)
    {
        var result = await _admin.ChangeRoleAsync(id, req.Role);
        return result.Success ? Ok(result) : MapFailure(result);
    }

    [HttpPost("{id:guid}/ban")]
    public async Task<IActionResult> ToggleBan(Guid id)
    {
        var result = await _admin.ToggleBanAsync(id);
        return result.Success ? Ok(result) : MapFailure(result);
    }

    [HttpPut("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] AdminResetPasswordRequest req)
    {
        var result = await _admin.AdminResetPasswordAsync(id, req.NewPassword);
        return result.Success ? Ok(result) : MapFailure(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _admin.DeleteUserAsync(id);
        return result.Success ? Ok(result) : MapFailure(result);
    }
}
