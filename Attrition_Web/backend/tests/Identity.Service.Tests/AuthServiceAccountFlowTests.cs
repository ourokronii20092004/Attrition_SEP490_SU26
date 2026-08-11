using Identity.Service.DTOs;
using Identity.Service.Repositories.Interface;
using Identity.Service.Services;
using Identity.Service.Services.Interface;
using NSubstitute;

namespace Identity.Service.Tests;

public class AuthServiceAccountFlowTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IEmailService _email = Substitute.For<IEmailService>();

    [Fact]
    public async Task Logout_UTCID01_ExistingToken_IsCleared()
    {
        var user = IdentityTestFixture.User(); _users.GetByIdAsync(user.Id).Returns(user);
        var result = await IdentityTestFixture.Auth(_users).LogoutAsync(user.Id);
        Assert.True(result.Success); Assert.Null(user.Refresh.Token); Assert.Null(user.Refresh.ExpiresAt);
        await _users.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task Logout_UTCID02_AlreadyNull_IsIdempotentlyCleared()
    {
        var user = IdentityTestFixture.User(); user.Refresh = new(); _users.GetByIdAsync(user.Id).Returns(user);
        Assert.True((await IdentityTestFixture.Auth(_users).LogoutAsync(user.Id)).Success);
        Assert.Null(user.Refresh.Token); await _users.Received(1).UpdateAsync(user);
    }

    [Theory]
    [InlineData("UTCID03")]
    [InlineData("UTCID04")]
    public async Task Logout_UnknownUser_FailsWithoutUpdate(string _)
    {
        var result = await IdentityTestFixture.Auth(_users).LogoutAsync(Guid.NewGuid());
        Assert.False(result.Success); Assert.Equal("User not found.", result.Error);
        await _users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Fact]
    public async Task ChangePassword_UTCID01_ChangesPasswordInvalidatesSessionsAndEmails()
    {
        var user = IdentityTestFixture.User(); _users.GetByIdAsync(user.Id).Returns(user); var before = DateTime.UtcNow;
        var result = await IdentityTestFixture.Auth(_users, _email).ChangePasswordAsync(user.Id,
            new ChangePasswordRequest(IdentityTestFixture.Password, IdentityTestFixture.NewPassword));
        Assert.True(result.Success); Assert.True(BCrypt.Net.BCrypt.Verify(IdentityTestFixture.NewPassword, user.PasswordHash));
        Assert.InRange(user.Security.TokensValidAfter!.Value, before, DateTime.UtcNow);
        Assert.Equal(TokenService.HashToken(result.Data!.RefreshToken), user.Refresh.Token);
        await _email.Received(1).SendAsync(user.Email!, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task ChangePassword_UTCID02_NoEmail_ChangesPasswordWithoutNotification()
    {
        var user = IdentityTestFixture.User(email: null); _users.GetByIdAsync(user.Id).Returns(user);
        var result = await IdentityTestFixture.Auth(_users, _email).ChangePasswordAsync(user.Id,
            new ChangePasswordRequest(IdentityTestFixture.Password, IdentityTestFixture.NewPassword));
        Assert.True(result.Success); await _email.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!);
    }

    [Fact]
    public async Task ChangePassword_UTCID03_WrongCurrentPassword_FailsWithoutUpdate()
    {
        var user = IdentityTestFixture.User(); _users.GetByIdAsync(user.Id).Returns(user);
        var result = await IdentityTestFixture.Auth(_users).ChangePasswordAsync(user.Id, new("wrong", IdentityTestFixture.NewPassword));
        Assert.False(result.Success); Assert.Equal("Incorrect current password.", result.Error);
        await _users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Fact]
    public async Task ChangePassword_UTCID04_NoPasswordHash_Fails()
    {
        var user = IdentityTestFixture.User(password: null); _users.GetByIdAsync(user.Id).Returns(user);
        var result = await IdentityTestFixture.Auth(_users).ChangePasswordAsync(user.Id, new("anything", IdentityTestFixture.NewPassword));
        Assert.False(result.Success); Assert.Equal("Incorrect current password.", result.Error);
    }

    [Theory]
    [InlineData("UTCID05")]
    [InlineData("UTCID06")]
    public async Task ChangePassword_UnknownUser_Fails(string _)
    {
        var result = await IdentityTestFixture.Auth(_users).ChangePasswordAsync(Guid.NewGuid(), new("anything", IdentityTestFixture.NewPassword));
        Assert.False(result.Success); Assert.Equal("User not found.", result.Error);
    }

    [Fact]
    public async Task VerifyEmail_UTCID01_ValidToken_VerifiesAndClearsToken() => await VerifyValid(null, false);

    [Fact]
    public async Task VerifyEmail_UTCID02_PendingEmail_PromotesIt() => await VerifyValid("new@fpt.edu.vn", false);

    [Fact]
    public async Task VerifyEmail_UTCID03_AlreadyVerified_RemainsVerifiedAndClearsToken() => await VerifyValid(null, true);

    private async Task VerifyValid(string? pending, bool alreadyVerified)
    {
        const string raw = "raw-token"; var user = IdentityTestFixture.User();
        user.PendingEmail = pending; user.IsEmailVerified = alreadyVerified;
        user.EmailVerification = new() { Token = TokenService.HashToken(raw), ExpiresAt = DateTime.UtcNow.AddHours(1) };
        _users.GetByEmailVerificationTokenAsync(TokenService.HashToken(raw)).Returns(user);
        var result = await IdentityTestFixture.Auth(_users).VerifyEmailAsync(new(raw));
        Assert.True(result.Success); Assert.True(user.IsEmailVerified); Assert.Null(user.PendingEmail);
        Assert.Null(user.EmailVerification.Token); if (pending != null) Assert.Equal(pending, user.Email);
    }

    [Theory]
    [InlineData("UTCID04", false)]
    [InlineData("UTCID07", true)]
    public async Task VerifyEmail_ExpiredToken_FailsWithoutMutation(string _, bool pending)
    {
        const string raw = "raw-token"; var user = IdentityTestFixture.User();
        user.IsEmailVerified = false;
        user.PendingEmail = pending ? "new@fpt.edu.vn" : null;
        user.EmailVerification = new() { Token = TokenService.HashToken(raw), ExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
        _users.GetByEmailVerificationTokenAsync(TokenService.HashToken(raw)).Returns(user);
        var result = await IdentityTestFixture.Auth(_users).VerifyEmailAsync(new(raw));
        Assert.False(result.Success); Assert.Contains("expired", result.Error!); Assert.False(user.IsEmailVerified);
        await _users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Fact]
    public async Task VerifyEmail_UTCID05_UnknownToken_Fails()
    {
        var result = await IdentityTestFixture.Auth(_users).VerifyEmailAsync(new("unknown"));
        Assert.False(result.Success); Assert.Equal("Invalid verification token.", result.Error);
    }

    [Fact]
    public async Task VerifyEmail_UTCID06_EmptyToken_FailsBeforeLookup()
    {
        var result = await IdentityTestFixture.Auth(_users).VerifyEmailAsync(new(""));
        Assert.False(result.Success); Assert.Equal("Verification token is required.", result.Error);
        await _users.DidNotReceiveWithAnyArgs().GetByEmailVerificationTokenAsync(default!);
    }

    [Fact]
    public async Task ResendVerification_UTCID01_UnverifiedEmail_ReplacesTokenAndQueuesEmail()
    {
        var user = IdentityTestFixture.User(); user.IsEmailVerified = false; user.EmailVerification.Token = "old";
        _users.GetByIdAsync(user.Id).Returns(user); var before = DateTime.UtcNow;
        var result = await IdentityTestFixture.Auth(_users, _email).SendVerificationEmailAsync(user.Id);
        Assert.True(result.Success); Assert.NotEqual("old", user.EmailVerification.Token);
        Assert.InRange(user.EmailVerification.ExpiresAt!.Value, before.AddHours(24), DateTime.UtcNow.AddHours(24));
        await _email.Received(1).SendAsync(user.Email!, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task ResendVerification_UTCID02_AlreadyVerified_Fails()
    {
        var user = IdentityTestFixture.User(); _users.GetByIdAsync(user.Id).Returns(user);
        var result = await IdentityTestFixture.Auth(_users).SendVerificationEmailAsync(user.Id);
        Assert.False(result.Success); Assert.Equal("Email is already verified.", result.Error);
    }

    [Fact]
    public async Task ResendVerification_UTCID03_NoEmail_Fails()
    {
        var user = IdentityTestFixture.User(null); user.IsEmailVerified = false; _users.GetByIdAsync(user.Id).Returns(user);
        var result = await IdentityTestFixture.Auth(_users).SendVerificationEmailAsync(user.Id);
        Assert.False(result.Success); Assert.Contains("No email", result.Error!);
    }

    [Theory]
    [InlineData("UTCID04")]
    [InlineData("UTCID05")]
    public async Task ResendVerification_UnknownUser_Fails(string _)
    {
        var result = await IdentityTestFixture.Auth(_users).SendVerificationEmailAsync(Guid.NewGuid());
        Assert.False(result.Success); Assert.Equal("User not found.", result.Error);
    }
}