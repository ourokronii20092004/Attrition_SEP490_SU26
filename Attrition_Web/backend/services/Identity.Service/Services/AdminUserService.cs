using BuildingBlocks.Contracts;
using Identity.Service.DTOs;
using Identity.Service.Repositories;

namespace Identity.Service.Services;

public class AdminUserService : IAdminUserService
{
    private readonly IUserRepository _userRepo;

    public AdminUserService(IUserRepository userRepo) => _userRepo = userRepo;

    public async Task<PaginatedResponse<UserListItem>> ListUsersAsync(int page, int pageSize, string? search, string? sort)
    {
        Func<IQueryable<Models.User>, IOrderedQueryable<Models.User>> orderBy = sort switch
        {
            "username" => q => q.OrderBy(u => u.Username),
            "oldest" => q => q.OrderBy(u => u.JoinedAt),
            _ => q => q.OrderByDescending(u => u.JoinedAt)
        };

        var (items, total) = await _userRepo.GetPagedAsync(
            page, pageSize,
            filter: string.IsNullOrWhiteSpace(search)
                ? null
                : u => u.Username.ToLower().Contains(search.ToLower()),
            orderBy: orderBy);

        var dtos = items.Select(u => new UserListItem(u.Id, u.Username, u.Role, u.IsBanned, u.JoinedAt)).ToList();
        return new PaginatedResponse<UserListItem>(dtos, total, page, pageSize);
    }

    public async Task<ApiResponse> ChangeRoleAsync(Guid userId, string role)
    {
        if (role != Roles.User && role != Roles.Admin) return ApiResponse.Fail("Invalid role.");
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse.Fail("User not found.");
        user.Role = role;
        await _userRepo.UpdateAsync(user);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> ToggleBanAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse.Fail("User not found.");
        user.IsBanned = !user.IsBanned;
        if (user.IsBanned)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiresAt = null;
        }
        await _userRepo.UpdateAsync(user);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> AdminResetPasswordAsync(Guid userId, string newPassword)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse.Fail("User not found.");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.MustChangePassword = true;
        user.RefreshToken = null;
        user.RefreshTokenExpiresAt = null;
        await _userRepo.UpdateAsync(user);
        return ApiResponse.Ok();
    }

    public async Task<ApiResponse> DeleteUserAsync(Guid userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null) return ApiResponse.Fail("User not found.");
        await _userRepo.DeleteAsync(user);
        return ApiResponse.Ok();
    }

    public async Task<List<UserSummaryDto>> SearchAsync(string query, int limit)
    {
        var users = await _userRepo.SearchByUsernameAsync(query, limit);
        return users.Select(ToSummary).ToList();
    }

    public async Task<List<UserSummaryDto>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.Distinct().ToList();
        var result = new List<UserSummaryDto>();
        foreach (var id in idList)
        {
            var u = await _userRepo.GetByIdAsync(id);
            if (u != null) result.Add(ToSummary(u));
        }
        return result;
    }

    public Task<int> CountAsync() => _userRepo.CountAsync();

    private static UserSummaryDto ToSummary(Models.User u) =>
        new(u.Id, u.Username, u.DisplayName, u.AvatarPath ?? u.GoogleAvatarUrl, u.Role);
}
