using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using api.Data;
using api.Models;
using api.Services;

namespace api.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<ILogger<UserService>> _loggerMock;
    private ApplicationDbContext _context = null!;
    private UserService _userService = null!;

    public UserServiceTests()
    {
        _loggerMock = new Mock<ILogger<UserService>>();
    }

    private void InitializeContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _userService = new UserService(_context, _loggerMock.Object);
    }

    #region IsEmailAvailableAsync Tests

    // 이메일 사용 가능 여부 - 실제 유저 테이블에 이메일이 존재하지 않을 때
    [Fact]
    public async Task IsEmailAvailableAsync_ShouldReturnTrue_WhenEmailDoesNotExist()
    {
        // Given
        InitializeContext();

        // When
        var isAvailable = await _userService.IsEmailAvailableAsync("test@test.com");

        // Then
        Assert.True(isAvailable);
    }

    // 이메일 사용 가능 여부 - 실제 유저 테이블에 이메일이 존재할 때
    [Fact]
    public async Task IsEmailAvailableAsync_ShouldReturnFalse_WhenEmailExistsInUsers()
    {
        // Given
        InitializeContext();

        // When
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            Password = "hashedpassword",
            Nickname = "testuser",
            Phone = "010-1234-5678",
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(existingUser);
        await _context.SaveChangesAsync();

        // Then
        var isAvailable = await _userService.IsEmailAvailableAsync("test@test.com");
        Assert.False(isAvailable);
    }

    #endregion

    #region IsNicknameAvailableAsync Tests

    // 닉네임 사용 가능 여부 - 실제 유저 테이블에 닉네임이 존재하지 않을 때
    [Fact]
    public async Task IsNicknameAvailableAsync_ShouldReturnTrue_WhenNicknameDoesNotExist()
    {
        // Given
        InitializeContext();

        // When
        var isAvailable = await _userService.IsNicknameAvailableAsync("newnickname");

        // Then
        Assert.True(isAvailable);
    }

    // 실제 유저 테이블에 닉네임이 존재할 때
    [Fact]
    public async Task IsNicknameAvailableAsync_ShouldReturnFalse_WhenNicknameExistsInUsers()
    {
        // Given
        InitializeContext();

        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            Password = "hashedpassword",
            Nickname = "existingnick",
            Phone = "010-1234-5678",
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(existingUser);
        await _context.SaveChangesAsync();

        // When
        var isAvailable = await _userService.IsNicknameAvailableAsync("existingnick");

        // Then
        Assert.False(isAvailable);
    }

    #endregion
}
