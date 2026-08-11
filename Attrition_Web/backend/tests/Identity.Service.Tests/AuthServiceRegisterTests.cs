using Identity.Service.DTOs;
using Identity.Service.Models;
using Identity.Service.Repositories.Interface;
using Identity.Service.Services;
using Identity.Service.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Identity.Service.Tests;

/// <summary>
/// Unit tests for <see cref="AuthService.RegisterAsync"/> — sheet "Register" (function code RE) of
/// Report5_Web_Unit_Test. Each test maps to one UTCID column of that sheet.
///
/// Test requirement: verify that the username is trimmed and lower-cased, that the username and the
/// email must both be free, that the password is stored as a BCrypt hash, that a verification token
/// valid for 24 hours is created, and that a unique-index violation is mapped to the same generic
/// message (BR-01).
///
/// Precondition shared by every case: the identity database is reachable and the account "dangtt"
/// (dangtt@fpt.edu.vn) already exists.
/// </summary>
public class AuthServiceRegisterTests
{
    private const string TakenMessage = "That username or email is already in use.";
    private const string ExistingUsername = "dangtt";
    private const string ExistingEmail = "dangtt@fpt.edu.vn";

    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IEmailService _email = Substitute.For<IEmailService>();
    private readonly AuthService _sut;

    public AuthServiceRegisterTests()
    {
        // ponytail: the repository is stubbed rather than backed by a real provider, so these tests
        // cover RegisterAsync's own logic only — the unique index itself is exercised by the
        // DbUpdateException path below (UTCID07) and, end to end, by the integration suite.
        // Upgrade path: swap the substitute for an EF Core Sqlite in-memory IdentityDbContext +
        // UserRepository if the mapping/index needs covering here too.
        _userRepo.IsUsernameAvailableAsync(Arg.Any<string>())
            .Returns(call => Task.FromResult(!string.Equals(call.Arg<string>(), ExistingUsername,
                StringComparison.OrdinalIgnoreCase)));

        _userRepo.GetByEmailAsync(Arg.Any<string>())
            .Returns(call => Task.FromResult(
                string.Equals(call.Arg<string>(), ExistingEmail, StringComparison.OrdinalIgnoreCase)
                    ? new User { Username = ExistingUsername, Email = ExistingEmail }
                    : null));

        _userRepo.AddAsync(Arg.Any<User>()).Returns(call => Task.FromResult(call.Arg<User>()));

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "unit-test-signing-secret-that-is-long-enough-for-hmac-sha256",
            ["Jwt:Issuer"] = "attrition-tests",
            ["Jwt:Audience"] = "attrition-tests",
            ["Jwt:AccessTokenExpiryMinutes"] = "15",
            ["Jwt:RefreshTokenExpiryDays"] = "7",
            ["App:ClientUrl"] = "http://localhost:3000",
        }).Build();

        _sut = new AuthService(_userRepo, config, _email, new TokenService(config),
            NullLogger<AuthService>.Instance, Substitute.For<IHttpClientFactory>());
    }

    /// <summary>Captures the entity handed to <c>AddAsync</c>, or null when nothing was inserted.</summary>
    private User? InsertedUser =>
        _userRepo.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IUserRepository.AddAsync))
            .Select(c => (User)c.GetArguments()[0]!)
            .LastOrDefault();

    // UTCID01 — Normal. A free username and a free email create the account, return a token pair
    // and queue the verification email.
    [Fact]
    public async Task UTCID01_FreeUsernameAndEmail_CreatesUserAndQueuesVerificationEmail()
    {
        const string password = "Str0ng!Passw0rd";

        var result = await _sut.RegisterAsync(new RegisterRequest("newplayer", password, "new@fpt.edu.vn"));

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.NotNull(result.Data);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.Data.RefreshToken));
        Assert.Equal("newplayer", result.Data.User.Username);

        var inserted = InsertedUser;
        Assert.NotNull(inserted);
        Assert.Equal("newplayer", inserted!.Username);
        Assert.Equal("new@fpt.edu.vn", inserted.Email);
        Assert.False(inserted.IsEmailVerified);

        // The password is stored as a BCrypt hash, never in the clear.
        Assert.NotNull(inserted.PasswordHash);
        Assert.NotEqual(password, inserted.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(password, inserted.PasswordHash));

        // A verification token valid for 24 hours is created, and it is stored hashed.
        Assert.NotNull(inserted.EmailVerification.Token);
        Assert.NotNull(inserted.EmailVerification.ExpiresAt);
        Assert.Equal(DateTime.UtcNow.AddHours(24), inserted.EmailVerification.ExpiresAt!.Value,
            TimeSpan.FromMinutes(1));

        // The refresh token is stored hashed, not as the value handed to the caller.
        Assert.Equal(TokenService.HashToken(result.Data.RefreshToken), inserted.Refresh.Token);

        await _email.Received(1).SendAsync("new@fpt.edu.vn", Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>());
    }

    // UTCID02 — Abnormal. A username that differs from an existing one only by case is rejected,
    // because the stored value is lower-cased before the uniqueness check.
    [Fact]
    public async Task UTCID02_UsernameDiffersOnlyByCase_FailsWithGenericTakenMessage()
    {
        var result = await _sut.RegisterAsync(new RegisterRequest("DangTT", "Str0ng!Passw0rd", "new@fpt.edu.vn"));

        Assert.False(result.Success);
        Assert.Equal(TakenMessage, result.Error);
        Assert.Null(result.Data);
        Assert.Null(InsertedUser);
        await _email.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!);
    }

    // UTCID03 — Abnormal. A free username with an already registered email is rejected with the
    // same generic message, so registration can't be used to enumerate accounts (BR-01).
    [Fact]
    public async Task UTCID03_EmailAlreadyTaken_FailsWithSameGenericTakenMessage()
    {
        var result = await _sut.RegisterAsync(new RegisterRequest("newplayer", "Str0ng!Passw0rd", ExistingEmail));

        Assert.False(result.Success);
        Assert.Equal(TakenMessage, result.Error);
        Assert.Null(result.Data);
        Assert.Null(InsertedUser);
        await _email.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!);
    }

    // UTCID04 — Boundary. Registering without an email succeeds and sends no verification email.
    [Fact]
    public async Task UTCID04_NullEmail_CreatesUserWithoutEmailAndSendsNoVerification()
    {
        var result = await _sut.RegisterAsync(new RegisterRequest("newplayer", "Str0ng!Passw0rd", null));

        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var inserted = InsertedUser;
        Assert.NotNull(inserted);
        Assert.Null(inserted!.Email);
        Assert.False(inserted.IsEmailVerified);

        await _email.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!);
        // A null email must not be probed for uniqueness.
        await _userRepo.DidNotReceiveWithAnyArgs().GetByEmailAsync(default!);
    }

    // UTCID05 — Boundary. A padded username is trimmed before the uniqueness check, so the check
    // runs against the normalized value and that value is what gets stored. (Defect DFID-W-04)
    [Fact]
    public async Task UTCID05_PaddedUsername_IsTrimmedBeforeUniquenessCheck()
    {
        var result = await _sut.RegisterAsync(new RegisterRequest("  newplayer  ", "Str0ng!Passw0rd", "new@fpt.edu.vn"));

        Assert.True(result.Success);
        Assert.Equal("newplayer", InsertedUser?.Username);
        await _userRepo.Received(1).IsUsernameAvailableAsync("newplayer");
    }

    // UTCID06 — Boundary. A padded email is trimmed before the uniqueness check, so the padded form
    // of an existing address is recognized as the same address. (Defect DFID-W-04)
    [Fact]
    public async Task UTCID06_PaddedEmail_IsTrimmedBeforeUniquenessCheck()
    {
        var result = await _sut.RegisterAsync(new RegisterRequest("newplayer", "Str0ng!Passw0rd", "  new@fpt.edu.vn  "));

        Assert.True(result.Success);
        Assert.Equal("new@fpt.edu.vn", InsertedUser?.Email);
        await _userRepo.Received(1).GetByEmailAsync("new@fpt.edu.vn");

        // The padded form of a taken address must resolve to that address and be rejected.
        var paddedTaken = await _sut.RegisterAsync(
            new RegisterRequest("newplayer", "Str0ng!Passw0rd", $"  {ExistingEmail}  "));
        Assert.False(paddedTaken.Success);
        Assert.Equal(TakenMessage, paddedTaken.Error);
    }

    // UTCID07 — Abnormal. A concurrent registration wins the unique-index race after the checks
    // pass; the resulting DbUpdateException is mapped to the same generic message, not a 500.
    [Fact]
    public async Task UTCID07_ConcurrentDuplicateInsert_MapsDbUpdateExceptionToTakenMessage()
    {
        _userRepo.AddAsync(Arg.Any<User>()).ThrowsAsync(new DbUpdateException("duplicate key"));

        var result = await _sut.RegisterAsync(new RegisterRequest("newplayer", "Str0ng!Passw0rd", "new@fpt.edu.vn"));

        Assert.False(result.Success);
        Assert.Equal(TakenMessage, result.Error);
        Assert.Null(result.Data);
        // The failed insert must not leave a verification email behind.
        await _email.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!);
    }

    // UTCID08 — Abnormal. A taken username plus a taken email, with a duplicate row inserted first:
    // the pre-insert checks short-circuit, so the same generic message comes back without an insert
    // ever being attempted.
    [Fact]
    public async Task UTCID08_TakenUsernameAndEmailWithConcurrentInsert_FailsBeforeAttemptingInsert()
    {
        _userRepo.AddAsync(Arg.Any<User>()).ThrowsAsync(new DbUpdateException("duplicate key"));

        var result = await _sut.RegisterAsync(new RegisterRequest("DangTT", "Str0ng!Passw0rd", ExistingEmail));

        Assert.False(result.Success);
        Assert.Equal(TakenMessage, result.Error);
        await _userRepo.DidNotReceiveWithAnyArgs().AddAsync(default!);
        await _email.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!);
    }
}