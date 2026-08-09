using Identity.Service.Repositories.Interface;
using Identity.Service.Services.Interface;
using NSubstitute;

namespace Identity.Service.Tests;

public class AccountServiceProfileTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IEmailService _email = Substitute.For<IEmailService>();

    [Fact]
    public async Task View_UTCID01_FullProfile_MapsPublicFields()
    {
        var user = IdentityTestFixture.User(); user.DisplayName = "Dang"; user.AvatarPath = "/avatar.png";
        user.BackgroundUrl = "/background.png"; user.Bio = "bio"; _users.GetByUsernameAsync("dangtt").Returns(user);
        var result = await IdentityTestFixture.Account(_users).GetProfileByUsernameAsync("dangtt");
        Assert.True(result.Success); Assert.Equal(user.Id, result.Data!.Id); Assert.Equal("Dang", result.Data.DisplayName);
        Assert.Equal("/avatar.png", result.Data.AvatarUrl); Assert.Equal("bio", result.Data.Bio);
    }

    [Fact]
    public async Task View_UTCID02_OptionalFieldsEmpty_ReturnsNulls()
    {
        var user = IdentityTestFixture.User(); _users.GetByUsernameAsync("dangtt").Returns(user);
        var result = await IdentityTestFixture.Account(_users).GetProfileByUsernameAsync("dangtt");
        Assert.True(result.Success); Assert.Null(result.Data!.DisplayName); Assert.Null(result.Data.AvatarUrl); Assert.Null(result.Data.Bio);
    }

    [Theory]
    [InlineData("UTCID03", "unknown")]
    [InlineData("UTCID04", "")]
    public async Task View_UnknownUsername_Fails(string _, string username)
    {
        var result = await IdentityTestFixture.Account(_users).GetProfileByUsernameAsync(username);
        Assert.False(result.Success); Assert.Equal("User not found.", result.Error);
    }

    [Fact]
    public async Task Update_UTCID01_BioAndDisplayName_AreStoredTrimmed()
    {
        var user = IdentityTestFixture.User(); _users.GetByIdAsync(user.Id).Returns(user);
        var result = await IdentityTestFixture.Account(_users).UpdateProfileAsync(user.Id,
            new("new bio", null, null, null, "  Wanderer  "));
        Assert.True(result.Success); Assert.Equal("new bio", user.Bio); Assert.Equal("Wanderer", user.DisplayName);
    }

    [Fact]
    public async Task Update_UTCID02_BlankDisplayName_IsStoredAsNull()
    {
        var user = IdentityTestFixture.User(); user.DisplayName = "old"; _users.GetByIdAsync(user.Id).Returns(user);
        Assert.True((await IdentityTestFixture.Account(_users).UpdateProfileAsync(user.Id, new(null, null, null, null, "   "))).Success);
        Assert.Null(user.DisplayName);
    }

    [Fact]
    public async Task Update_UTCID03_OmittedDisplayName_IsUnchanged()
    {
        var user = IdentityTestFixture.User(); user.DisplayName = "old"; _users.GetByIdAsync(user.Id).Returns(user);
        await IdentityTestFixture.Account(_users).UpdateProfileAsync(user.Id, new("bio", null, null, null, null));
        Assert.Equal("old", user.DisplayName);
    }

    [Fact]
    public async Task Update_UTCID04_FreeEmail_StoresPendingAddressAndEmails()
    {
        var user = IdentityTestFixture.User(); _users.GetByIdAsync(user.Id).Returns(user);
        var result = await IdentityTestFixture.Account(_users, _email).UpdateEmailAsync(user.Id,
            new("new@fpt.edu.vn", IdentityTestFixture.Password));
        Assert.True(result.Success); Assert.Equal("new@fpt.edu.vn", user.PendingEmail); Assert.NotNull(user.EmailVerification.Token);
        await _email.Received(1).SendAsync("new@fpt.edu.vn", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task Update_UTCID05_WrongPassword_RejectsEmailChange()
    {
        var user = IdentityTestFixture.User(); _users.GetByIdAsync(user.Id).Returns(user);
        var result = await IdentityTestFixture.Account(_users).UpdateEmailAsync(user.Id, new("new@fpt.edu.vn", "wrong"));
        Assert.False(result.Success); Assert.Equal("Incorrect current password.", result.Error); Assert.Null(user.PendingEmail);
    }

    [Fact]
    public async Task Update_UTCID06_EmailUsedByOtherAccount_IsRejected()
    {
        var user = IdentityTestFixture.User(); _users.GetByIdAsync(user.Id).Returns(user);
        _users.GetByEmailAsync("used@fpt.edu.vn").Returns(IdentityTestFixture.User("used@fpt.edu.vn"));
        var result = await IdentityTestFixture.Account(_users).UpdateEmailAsync(user.Id,
            new("used@fpt.edu.vn", IdentityTestFixture.Password));
        Assert.False(result.Success); Assert.Contains("already in use", result.Error!);
    }

    [Fact]
    public async Task Update_UTCID07_GoogleOnlyAccount_MustSetPasswordFirst()
    {
        var user = IdentityTestFixture.User(password: null); _users.GetByIdAsync(user.Id).Returns(user);
        var result = await IdentityTestFixture.Account(_users).UpdateEmailAsync(user.Id, new("new@fpt.edu.vn", "anything"));
        Assert.False(result.Success); Assert.Contains("set a password", result.Error!);
    }

    [Theory]
    [InlineData("UTCID08")]
    [InlineData("UTCID09")]
    public async Task Update_DeletionRequest_StoresTwentyFourHourTokenAndEmails(string _)
    {
        var user = IdentityTestFixture.User(); _users.GetByIdAsync(user.Id).Returns(user); var before = DateTime.UtcNow;
        var result = await IdentityTestFixture.Account(_users, _email).RequestDeletionAsync(user.Id);
        Assert.True(result.Success); Assert.NotNull(user.DeletionConfirm.Token);
        Assert.InRange(user.DeletionConfirm.ExpiresAt!.Value, before.AddHours(24), DateTime.UtcNow.AddHours(24));
        await _email.Received(1).SendAsync(user.Email!, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>());
    }

    [Theory]
    [InlineData(false, false, "UTCID01")]
    [InlineData(true, true, "UTCID02")]
    [InlineData(false, null, "UTCID03")]
    [InlineData(null, false, "UTCID04")]
    [InlineData(null, null, "UTCID05")]
    public async Task NotificationPreferences_PatchOnlySuppliedFlags(bool? reply, bool? mention, string _)
    {
        var user = IdentityTestFixture.User(); user.NotifyOnReply = true; user.NotifyOnMention = true;
        _users.GetByIdAsync(user.Id).Returns(user); var before = user.UpdatedAt;
        var result = await IdentityTestFixture.Account(_users).UpdateProfileAsync(user.Id,
            new(null, null, reply, mention, null));
        Assert.True(result.Success); Assert.Equal(reply ?? true, user.NotifyOnReply);
        Assert.Equal(mention ?? true, user.NotifyOnMention); Assert.True(user.UpdatedAt >= before);
    }

    [Fact]
    public async Task NotificationPreferences_UTCID06_UnknownUser_Fails()
    {
        var result = await IdentityTestFixture.Account(_users).UpdateProfileAsync(Guid.NewGuid(), new(null, null, false, false, null));
        Assert.False(result.Success); Assert.Equal("User not found.", result.Error);
    }
}