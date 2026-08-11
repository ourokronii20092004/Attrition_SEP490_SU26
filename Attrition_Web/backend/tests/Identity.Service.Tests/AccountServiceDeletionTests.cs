using Identity.Service.Repositories.Interface;
using Identity.Service.Services;
using Identity.Service.Services.Interface;
using NSubstitute;

namespace Identity.Service.Tests;

public class AccountServiceDeletionTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IEmailService _email = Substitute.For<IEmailService>();

    [Fact]
    public async Task UTCID01_Request_StoresTwentyFourHourTokenAndQueuesEmail()
    {
        var user = IdentityTestFixture.User(); _users.GetByIdAsync(user.Id).Returns(user); var before = DateTime.UtcNow;
        var result = await IdentityTestFixture.Account(_users, _email).RequestDeletionAsync(user.Id);
        Assert.True(result.Success); Assert.NotNull(user.DeletionConfirm.Token);
        Assert.InRange(user.DeletionConfirm.ExpiresAt!.Value, before.AddHours(24), DateTime.UtcNow.AddHours(24));
        await _email.Received(1).SendAsync(user.Email!, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task UTCID02_Request_AlreadyScheduled_Fails()
    {
        var user = IdentityTestFixture.User(); user.IsDeleted = true; _users.GetByIdAsync(user.Id).Returns(user);
        var result = await IdentityTestFixture.Account(_users).RequestDeletionAsync(user.Id);
        Assert.False(result.Success); Assert.Equal("This account is already scheduled for deletion.", result.Error);
    }

    [Fact]
    public async Task UTCID03_Request_NoEmail_Fails()
    {
        var user = IdentityTestFixture.User(null); _users.GetByIdAsync(user.Id).Returns(user);
        var result = await IdentityTestFixture.Account(_users).RequestDeletionAsync(user.Id);
        Assert.False(result.Success); Assert.Contains("Add and verify an email", result.Error!);
    }

    [Fact]
    public async Task UTCID04_Confirm_ValidToken_SoftDeletesAndRevokesRefresh()
    {
        const string raw = "deletion-token"; var user = IdentityTestFixture.User();
        user.DeletionConfirm = new() { Token = TokenService.HashToken(raw), ExpiresAt = DateTime.UtcNow.AddHours(1) };
        _users.GetByIdAsync(user.Id).Returns(user); var before = DateTime.UtcNow;
        var result = await IdentityTestFixture.Account(_users).ConfirmDeletionAsync(user.Id, raw);
        Assert.True(result.Success); Assert.True(user.IsDeleted);
        Assert.InRange(user.DeletedAt!.Value, before, DateTime.UtcNow);
        Assert.Null(user.DeletionConfirm.Token); Assert.Null(user.Refresh.Token);
        await _users.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task UTCID05_Confirm_ExpiredToken_FailsWithoutDeletion() =>
        await AssertInvalid("raw", TokenService.HashToken("raw"), DateTime.UtcNow.AddMinutes(-1));

    [Fact]
    public async Task UTCID06_Confirm_WrongToken_FailsWithoutDeletion() =>
        await AssertInvalid("wrong", TokenService.HashToken("right"), DateTime.UtcNow.AddMinutes(10));

    [Fact]
    public async Task UTCID07_Confirm_NoStoredToken_FailsWithoutDeletion() =>
        await AssertInvalid("raw", null, null);

    private async Task AssertInvalid(string supplied, string? stored, DateTime? expiry)
    {
        var user = IdentityTestFixture.User(); user.DeletionConfirm = new() { Token = stored, ExpiresAt = expiry };
        _users.GetByIdAsync(user.Id).Returns(user);
        var result = await IdentityTestFixture.Account(_users).ConfirmDeletionAsync(user.Id, supplied);
        Assert.False(result.Success); Assert.Equal("This confirmation link is invalid or has expired.", result.Error);
        Assert.False(user.IsDeleted); await _users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }
}