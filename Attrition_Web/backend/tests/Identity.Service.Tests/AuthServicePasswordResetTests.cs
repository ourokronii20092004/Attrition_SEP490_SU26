using Identity.Service.Models;
using Identity.Service.Repositories.Interface;
using Identity.Service.Services;
using Identity.Service.Services.Interface;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Identity.Service.Tests;

public class AuthServicePasswordResetTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IEmailService _email = Substitute.For<IEmailService>();

    [Fact]
    public async Task Request_UTCID01_KnownEmail_StoresOneHourTokenAndEmails()
    {
        var user = IdentityTestFixture.User(); _users.GetByEmailAsync(user.Email!).Returns(user); var before = DateTime.UtcNow;
        var result = await IdentityTestFixture.Auth(_users, _email).ForgotPasswordAsync(new(user.Email!));
        Assert.True(result.Success); Assert.NotNull(user.PasswordReset.Token);
        Assert.InRange(user.PasswordReset.ExpiresAt!.Value, before.AddHours(1), DateTime.UtcNow.AddHours(1));
        await _users.Received(1).UpdateAsync(user);
        await _email.Received(1).SendAsync(user.Email!, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>());
    }

    [Theory]
    [InlineData("UTCID02")]
    [InlineData("UTCID06")]
    public async Task Request_UnknownEmail_ReturnsGenericSuccessWithoutMutation(string _)
    {
        _users.GetByEmailAsync("unknown@fpt.edu.vn").Returns((User?)null);
        var result = await IdentityTestFixture.Auth(_users, _email).ForgotPasswordAsync(new("unknown@fpt.edu.vn"));
        Assert.True(result.Success); await _users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
        await _email.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!);
    }

    [Fact]
    public async Task Request_UTCID03_EmptyEmail_FailsBeforeLookup()
    {
        var result = await IdentityTestFixture.Auth(_users).ForgotPasswordAsync(new(""));
        Assert.False(result.Success); Assert.Equal("Email is required.", result.Error);
        await _users.DidNotReceiveWithAnyArgs().GetByEmailAsync(default!);
    }

    [Fact]
    public async Task Request_UTCID04_ExistingToken_IsReplaced()
    {
        var user = IdentityTestFixture.User(); user.PasswordReset.Token = "old"; _users.GetByEmailAsync(user.Email!).Returns(user);
        Assert.True((await IdentityTestFixture.Auth(_users, _email).ForgotPasswordAsync(new(user.Email!))).Success);
        Assert.NotEqual("old", user.PasswordReset.Token);
    }

    [Fact]
    public async Task Request_UTCID05_EmailFailure_IsBestEffort()
    {
        var user = IdentityTestFixture.User(); _users.GetByEmailAsync(user.Email!).Returns(user);
        _email.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>()).ThrowsAsync(new IOException());
        var result = await IdentityTestFixture.Auth(_users, _email).ForgotPasswordAsync(new(user.Email!));
        Assert.True(result.Success); Assert.NotNull(user.PasswordReset.Token);
    }

    [Theory]
    [InlineData("UTCID01", true, false)]
    [InlineData("UTCID02", false, false)]
    [InlineData("UTCID03", true, true)]
    public async Task Reset_ValidToken_RehashesPasswordAndInvalidatesSessions(string _, bool hasRefresh, bool samePassword)
    {
        const string raw = "raw-reset"; var user = IdentityTestFixture.User();
        if (!hasRefresh) user.Refresh = new();
        user.PasswordReset = new() { Token = TokenService.HashToken(raw), ExpiresAt = DateTime.UtcNow.AddMinutes(10) };
        _users.GetByPasswordResetTokenAsync(TokenService.HashToken(raw)).Returns(user); var before = DateTime.UtcNow;
        var password = samePassword ? IdentityTestFixture.Password : IdentityTestFixture.NewPassword;
        var result = await IdentityTestFixture.Auth(_users).ResetPasswordAsync(new(raw, password));
        Assert.True(result.Success); Assert.True(BCrypt.Net.BCrypt.Verify(password, user.PasswordHash));
        Assert.Null(user.PasswordReset.Token); Assert.Null(user.Refresh.Token);
        Assert.InRange(user.Security.TokensValidAfter!.Value, before, DateTime.UtcNow);
    }

    [Fact]
    public async Task Reset_UTCID04_ExpiredToken_FailsWithoutMutation()
    {
        const string raw = "raw-reset"; var user = IdentityTestFixture.User(); var oldHash = user.PasswordHash;
        user.PasswordReset = new() { Token = TokenService.HashToken(raw), ExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
        _users.GetByPasswordResetTokenAsync(TokenService.HashToken(raw)).Returns(user);
        var result = await IdentityTestFixture.Auth(_users).ResetPasswordAsync(new(raw, IdentityTestFixture.NewPassword));
        Assert.False(result.Success); Assert.Equal(oldHash, user.PasswordHash);
    }

    [Fact]
    public async Task Reset_UTCID05_UnknownToken_Fails()
    {
        var result = await IdentityTestFixture.Auth(_users).ResetPasswordAsync(new("unknown", IdentityTestFixture.NewPassword));
        Assert.False(result.Success); Assert.Equal("Invalid or expired password reset token.", result.Error);
    }

    [Fact]
    public async Task Reset_UTCID06_EmptyToken_FailsBeforeLookup()
    {
        var result = await IdentityTestFixture.Auth(_users).ResetPasswordAsync(new("", IdentityTestFixture.NewPassword));
        Assert.False(result.Success); Assert.Equal("Reset token is required.", result.Error);
        await _users.DidNotReceiveWithAnyArgs().GetByPasswordResetTokenAsync(default!);
    }
}