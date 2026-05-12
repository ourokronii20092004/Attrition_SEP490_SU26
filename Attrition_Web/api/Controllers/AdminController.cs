using Attrition.API.DTOs;
using Attrition.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Attrition.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly AuthService _auth;
    public AdminController(AuthService auth) => _auth = auth;

    [HttpGet("users")]
    public async Task<IActionResult> ListUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _auth.ListUsersAsync(page, pageSize);
        return Ok(result);
    }

    [HttpPut("users/{id}/role")]
    public async Task<IActionResult> ChangeRole(Guid id, [FromBody] string role)
    {
        var result = await _auth.ChangeRoleAsync(id, role);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("users/{id}/ban")]
    public async Task<IActionResult> ToggleBan(Guid id)
    {
        var result = await _auth.ToggleBanAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("users/{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] string newPassword)
    {
        var result = await _auth.AdminResetPasswordAsync(id, newPassword);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}