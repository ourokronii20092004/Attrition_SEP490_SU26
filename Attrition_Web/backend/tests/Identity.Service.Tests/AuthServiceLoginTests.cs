using Identity.Service.DTOs;
using Identity.Service.Models;
using Identity.Service.Repositories.Interface;
using Identity.Service.Services.Interface;
using NSubstitute;

namespace Identity.Service.Tests;

public class AuthServiceLoginTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IEmailService _email = Substitute.For<IEmailService>();

    private async Task<(BuildingBlocks.Contracts.ApiResponse<AuthResponse> Result, User User)> Login(
        string password = IdentityTestFixture.Password, Action<User>? arrange = null)
    {
        var user = IdentityTestFixture.User();
        arrange?.Invoke(user);
        _users.GetByUsernameAsync("dangtt").Returns(user);
        var result = await IdentityTestFixture.Auth(_users, _email)
            .LoginAsync(new LoginRequest("  dangtt  ", password), "127.0.0.1");
        return (result, user);
    }

    [Fact]
    public async Task UTCID01_ValidCredentials_ReturnTokensAndClearFailures()
    {
        var (result, user) = await Login(arrange: u => u.Security.FailedLoginAttempts = 3);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.AccessToken));
        Assert.Equal(0, user.Security.FailedLoginAttempts);
        Assert.Null(user.Security.LockoutEnd);
        Assert.Equal("127.0.0.1", user.Security.LastLoginIp);
        await _users.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task UTCID02_WrongPassword_IncrementsCounterAndReturnsGenericFailure()
    {
        var (result, user) = await Login("wrong");
        Assert.False(result.Success);
        Assert.Equal("Invalid username or password.", result.Error);
        Assert.Equal(1, user.Security.FailedLoginAttempts);
        await _users.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task UTCID03_FifthWrongPassword_LocksForFifteenMinutes()
    {
        var before = DateTime.UtcNow;
        var (result, user) = await Login("wrong", u => u.Security.FailedLoginAttempts = 4);
        Assert.False(result.Success);
        Assert.Equal(5, user.Security.FailedLoginAttempts);
        Assert.InRange(user.Security.LockoutEnd!.Value, before.AddMinutes(15), DateTime.UtcNow.AddMinutes(15));
    }

    [Fact]
    public async Task UTCID04_UnknownUsername_ReturnsGenericFailureWithoutUpdate()
    {
        _users.GetByUsernameAsync("ghost").Returns((User?)null);
        var result = await IdentityTestFixture.Auth(_users, _email)
            .LoginAsync(new LoginRequest("ghost", IdentityTestFixture.Password), null);
        Assert.False(result.Success);
        Assert.Equal("Invalid username or password.", result.Error);
        await _users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Fact]
    public async Task UTCID05_UnverifiedEmail_StoresFreshTokenAndSendsLink()
    {
        var before = DateTime.UtcNow;
        var (result, user) = await Login(arrange: u => u.IsEmailVerified = false);
        Assert.False(result.Success);
        Assert.Contains("verify your email", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(user.EmailVerification.Token);
        Assert.InRange(user.EmailVerification.ExpiresAt!.Value, before.AddHours(24), DateTime.UtcNow.AddHours(24));
        await _email.Received(1).SendAsync(user.Email!, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task UTCID06_BannedAccount_ReturnsSuspendedWithoutIssuingSession()
    {
        var (result, user) = await Login(arrange: u => u.IsBanned = true);
        Assert.False(result.Success);
        Assert.Equal("Account is suspended.", result.Error);
        Assert.Equal("old-refresh", user.Refresh.Token);
        await _users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Fact]
    public async Task UTCID07_CorrectPasswordDuringLockout_ReturnsLockoutMessage()
    {
        var (result, _) = await Login(arrange: u => u.Security.LockoutEnd = DateTime.UtcNow.AddMinutes(5));
        Assert.False(result.Success);
        Assert.StartsWith("Account temporarily locked", result.Error);
    }

    [Fact]
    public async Task UTCID08_WrongPasswordDuringLockout_ReturnsGenericFailure()
    {
        var (result, _) = await Login("wrong", u => u.Security.LockoutEnd = DateTime.UtcNow.AddMinutes(5));
        Assert.False(result.Success);
        Assert.Equal("Invalid username or password.", result.Error);
    }

    [Fact]
    public async Task UTCID09_SoftDeletedAccount_IsRestoredAndSignedIn()
    {
        var (result, user) = await Login(arrange: u => { u.IsDeleted = true; u.DeletedAt = DateTime.UtcNow.AddDays(-1); });
        Assert.True(result.Success);
        Assert.False(user.IsDeleted);
        Assert.Null(user.DeletedAt);
    }

    [Fact]
    public async Task UTCID10_GoogleOnlyAccount_ReturnsGenericPasswordFailure()
    {
        var (result, user) = await Login(arrange: u => u.PasswordHash = null);
        Assert.False(result.Success);
        Assert.Equal("Invalid username or password.", result.Error);
        Assert.Equal(1, user.Security.FailedLoginAttempts);
    }
}