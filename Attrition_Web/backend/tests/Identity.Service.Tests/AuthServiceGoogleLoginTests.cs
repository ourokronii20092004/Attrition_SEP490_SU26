using BuildingBlocks.Contracts;
using Google.Apis.Auth;
using Identity.Service.DTOs;
using Identity.Service.Models;
using Identity.Service.Repositories.Interface;
using Identity.Service.Services;
using NSubstitute;
using System.Reflection;

namespace Identity.Service.Tests;

public class AuthServiceGoogleLoginTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();

    private async Task<ApiResponse<AuthResponse>> Issue(GoogleJsonWebSignature.Payload payload)
    {
        var sut = IdentityTestFixture.Auth(_users);
        var method = typeof(AuthService).GetMethod("IssueForGooglePayloadAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return await (Task<ApiResponse<AuthResponse>>)method.Invoke(sut, new object?[] { payload, "127.0.0.1" })!;
    }

    private static GoogleJsonWebSignature.Payload Payload(string email = "player.one@gmail.com", bool verified = true) => new()
    {
        Subject = "google-subject",
        Email = email,
        EmailVerified = verified,
        Name = "Player One",
        Picture = "https://example.test/avatar.png"
    };

    [Fact]
    public async Task UTCID01_ExistingGoogleId_SignsInExistingAccount()
    {
        var user = IdentityTestFixture.User(); user.GoogleId = "google-subject";
        _users.GetByGoogleIdAsync("google-subject").Returns(user);
        var result = await Issue(Payload());
        Assert.True(result.Success); Assert.Equal(user.Id, result.Data!.User.Id);
        await _users.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task UTCID02_VerifiedEmailMatch_LinksExistingPasswordAccount()
    {
        var user = IdentityTestFixture.User("player.one@gmail.com");
        _users.GetByEmailAsync(user.Email!).Returns(user);
        var result = await Issue(Payload());
        Assert.True(result.Success); Assert.Equal("google-subject", user.GoogleId);
        Assert.Equal("linked", user.AuthProvider); Assert.True(user.IsEmailVerified);
    }

    [Fact]
    public async Task UTCID03_UnverifiedGoogleEmailMatch_RequiresPasswordLinkFlow()
    {
        var user = IdentityTestFixture.User("player.one@gmail.com");
        _users.GetByEmailAsync(user.Email!).Returns(user);
        var result = await Issue(Payload(verified: false));
        Assert.False(result.Success); Assert.Contains("Sign in with your password", result.Error!);
        Assert.Null(user.GoogleId);
    }

    [Fact]
    public async Task UTCID04_NewGoogleUser_SanitizesEmailPrefix()
    {
        User? added = null; _users.IsUsernameAvailableAsync("playerone").Returns(true);
        _users.TryAddAsync(Arg.Do<User>(u => added = u)).Returns(true);
        var result = await Issue(Payload());
        Assert.True(result.Success); Assert.Equal("playerone", added!.Username); Assert.Equal("google", added.AuthProvider);
    }

    [Fact]
    public async Task UTCID05_ShortSanitizedPrefix_UsesUserFallback()
    {
        User? added = null; _users.IsUsernameAvailableAsync("user").Returns(true);
        _users.TryAddAsync(Arg.Do<User>(u => added = u)).Returns(true);
        Assert.True((await Issue(Payload("ab@gmail.com"))).Success);
        Assert.Equal("user", added!.Username);
    }

    [Fact]
    public async Task UTCID06_LongPrefix_IsTruncatedToTwentyCharacters()
    {
        User? added = null; _users.IsUsernameAvailableAsync(Arg.Any<string>()).Returns(true);
        _users.TryAddAsync(Arg.Do<User>(u => added = u)).Returns(true);
        Assert.True((await Issue(Payload("abcdefghijklmnopqrstuvwxyz@gmail.com"))).Success);
        Assert.Equal(20, added!.Username.Length); Assert.Equal("abcdefghijklmnopqrst", added.Username);
    }

    [Theory]
    [InlineData("UTCID07")]
    [InlineData("UTCID08")]
    public async Task InvalidJwt_ReturnsInvalidGoogleToken(string _)
    {
        // Malformed input is rejected locally by Google's validator; no network request is made.
        var result = await IdentityTestFixture.Auth(_users).GoogleLoginAsync(new("not-a-jwt", ""));
        Assert.False(result.Success); Assert.Equal("Invalid Google token.", result.Error);
    }

    [Fact]
    public async Task UTCID09_BannedGoogleAccount_IsRefused()
    {
        var user = IdentityTestFixture.User(); user.GoogleId = "google-subject"; user.IsBanned = true;
        _users.GetByGoogleIdAsync("google-subject").Returns(user);
        var result = await Issue(Payload());
        Assert.False(result.Success); Assert.Equal("Account is suspended.", result.Error);
    }
}