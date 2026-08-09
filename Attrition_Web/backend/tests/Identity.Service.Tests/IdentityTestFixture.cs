using Identity.Service.Models;
using Identity.Service.Repositories.Interface;
using Identity.Service.Services;
using Identity.Service.Services.Interface;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Identity.Service.Tests;

internal static class IdentityTestFixture
{
    internal const string Password = "Str0ng!Passw0rd";
    internal const string NewPassword = "N3w!Passw0rd";

    internal static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "unit-test-signing-secret-that-is-long-enough-for-hmac-sha256",
            ["Jwt:Issuer"] = "attrition-tests",
            ["Jwt:Audience"] = "attrition-tests",
            ["Jwt:AccessTokenExpiryMinutes"] = "15",
            ["Jwt:RefreshTokenExpiryDays"] = "7",
            ["App:ClientUrl"] = "http://localhost:3000",
        }).Build();

    internal static User User(string? email = "dangtt@fpt.edu.vn", string? password = Password) => new()
    {
        Username = "dangtt",
        Email = email,
        PasswordHash = password == null ? null : BCrypt.Net.BCrypt.HashPassword(password),
        IsEmailVerified = true,
        Refresh = new() { Token = "old-refresh", ExpiresAt = DateTime.UtcNow.AddDays(1) },
    };

    internal static AuthService Auth(IUserRepository users, IEmailService? email = null)
    {
        var config = Config();
        return new AuthService(users, config, email ?? Substitute.For<IEmailService>(),
            new TokenService(config), NullLogger<AuthService>.Instance,
            Substitute.For<IHttpClientFactory>());
    }

    internal static AccountService Account(IUserRepository users, IEmailService? email = null) =>
        new(users, Substitute.For<IFileService>(), email ?? Substitute.For<IEmailService>(),
            Config(), NullLogger<AccountService>.Instance);
}