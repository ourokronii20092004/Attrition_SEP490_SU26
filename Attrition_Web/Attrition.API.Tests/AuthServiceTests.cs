using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Attrition.API.DTOs;
using Attrition.API.Models;
using Attrition.API.Repositories;
using Attrition.API.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Attrition.API.Tests;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _configMock = new Mock<IConfiguration>();
        _emailServiceMock = new Mock<IEmailService>();

        // Set up mock configuration values for JWT
        _configMock.Setup(c => c["Jwt:Secret"]).Returns("dev-secret-key-change-in-production-must-be-32-chars");
        _configMock.Setup(c => c["Jwt:Issuer"]).Returns("attrition-api");
        _configMock.Setup(c => c["Jwt:Audience"]).Returns("attrition-web");
        _configMock.Setup(c => c["Jwt:AccessTokenExpiryMinutes"]).Returns("15");
        _configMock.Setup(c => c["Jwt:RefreshTokenExpiryDays"]).Returns("7");

        _authService = new AuthService(_userRepoMock.Object, _configMock.Object, _emailServiceMock.Object);
    }

    [Fact]
    public async Task RegisterAsync_ShouldCreateUser_WhenUsernameAndEmailAvailable()
    {
        // Arrange
        var request = new RegisterRequest("newUser", "new@example.com", "Password123!");
        _userRepoMock.Setup(r => r.IsUsernameAvailableAsync(request.Username)).ReturnsAsync(true);
        _userRepoMock.Setup(r => r.GetByEmailAsync(request.Email!)).ReturnsAsync((User?)null);
        _userRepoMock.Setup(r => r.AddAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);

        // Act
        var result = await _authService.RegisterAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("newUser", result.Data.User.Username);
        _userRepoMock.Verify(r => r.AddAsync(It.Is<User>(u => u.Username == "newUser")), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_ShouldFail_WhenUsernameTaken()
    {
        // Arrange
        var request = new RegisterRequest("takenUser", "new@example.com", "Password123!");
        _userRepoMock.Setup(r => r.IsUsernameAvailableAsync(request.Username)).ReturnsAsync(false);

        // Act
        var result = await _authService.RegisterAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Username is already taken.", result.Error);
    }

    [Fact]
    public async Task LoginAsync_ShouldSucceed_WhenCredentialsValid()
    {
        // Arrange
        var user = new User
        {
            Username = "validUser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!")
        };
        var request = new LoginRequest("validUser", "Password123!");
        _userRepoMock.Setup(r => r.GetByUsernameAsync(request.Username)).ReturnsAsync(user);

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockoutEnd);
    }

    [Fact]
    public async Task LoginAsync_ShouldLockout_AfterFiveFailedAttempts()
    {
        // Arrange
        var user = new User
        {
            Username = "testUser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword"),
            FailedLoginAttempts = 4
        };
        var request = new LoginRequest("testUser", "WrongPassword");
        _userRepoMock.Setup(r => r.GetByUsernameAsync(request.Username)).ReturnsAsync(user);

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(5, user.FailedLoginAttempts);
        Assert.NotNull(user.LockoutEnd);
        Assert.True(user.LockoutEnd > DateTime.UtcNow);
    }

    [Fact]
    public async Task LoginAsync_ShouldFail_WhenLockedOut()
    {
        // Arrange
        var user = new User
        {
            Username = "lockedUser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword"),
            LockoutEnd = DateTime.UtcNow.AddMinutes(10)
        };
        var request = new LoginRequest("lockedUser", "CorrectPassword");
        _userRepoMock.Setup(r => r.GetByUsernameAsync(request.Username)).ReturnsAsync(user);

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Account locked", result.Error);
    }
}
