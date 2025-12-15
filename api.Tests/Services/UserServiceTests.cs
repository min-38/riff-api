using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using api.Data;
using api.Models;
using api.Services;

namespace api.Tests.Services;

public class UserServiceTests : IDisposable
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

    public void Dispose()
    {
        _context?.Dispose(); // DB 컨텍스트 리소스 정리
    }

    #region 이메일 중복 테스트

    // 사용 가능한 이메일
    [Fact]
    public async Task IsEmailAvailableAsync_ShouldReturnTrue_WhenEmailDoesNotExist()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";

        // Act
        var result = await _userService.IsEmailAvailableAsync(email);

        // Assert
        Assert.True(result);
    }

    // 이메일이 이미 존재하는 경우
    [Fact]
    public async Task IsEmailAvailableAsync_ShouldReturnFalse_WhenEmailExists()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var user = new User
        {
            Email = email,
            Phone = "01012345678",
            Nickname = "test"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _userService.IsEmailAvailableAsync(email);

        // Assert
        Assert.False(result);
    }

    // 대소문자가 다른 이메일도 중복으로 처리
    [Fact]
    public async Task IsEmailAvailableAsync_ShouldBeCaseInsensitive()
    {
        // Arrange
        InitializeContext();
        var existingEmail = "Test@test.com";
        var checkEmail = "test@test.com";
        var user = new User
        {
            Email = existingEmail,
            Phone = "01012345678",
            Nickname = "test"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _userService.IsEmailAvailableAsync(checkEmail);

        // Assert
        Assert.False(result); // 대소문자가 달라도 중복으로 처리
    }

    #endregion

    #region 닉네임 중복 테스트

    // 닉네임이 사용 가능한 경우 (DB에 없음)
    [Fact]
    public async Task IsNicknameAvailableAsync_ShouldReturnTrue_WhenNicknameDoesNotExist()
    {
        // Arrange
        InitializeContext();
        var nickname = "newnickname";

        // Act
        var result = await _userService.IsNicknameAvailableAsync(nickname);

        // Assert
        Assert.True(result);
    }

    // 닉네임이 이미 존재하는 경우
    [Fact]
    public async Task IsNicknameAvailableAsync_ShouldReturnFalse_WhenNicknameExists()
    {
        // Arrange
        InitializeContext();
        var nickname = "existingnick";
        var user = new User
        {
            Email = "test@test.com",
            Phone = "01012345678",
            Nickname = nickname
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _userService.IsNicknameAvailableAsync(nickname);

        // Assert
        Assert.False(result);
    }

    // 대소문자가 다른 닉네임은 서로 다른 닉네임으로 처리
    [Fact]
    public async Task IsNicknameAvailableAsync_ShouldBeCaseSensitive()
    {
        // Arrange
        InitializeContext();
        var existingNickname = "TestNick";
        var checkNickname = "testnick";
        var user = new User
        {
            Email = "test@test.com",
            Phone = "01012345678",
            Nickname = existingNickname
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _userService.IsNicknameAvailableAsync(checkNickname);

        // Assert
        Assert.True(result); // 대소문자가 달라도 사용 가능
    }

    #endregion

    #region 핸드폰 번호 테스트

    // 전화번호로 사용자 조회 성공
    [Fact]
    public async Task GetUserByPhoneAsync_ShouldReturnUser_WhenUserExists()
    {
        // Arrange
        InitializeContext();
        var phone = "01012345678";
        var user = new User
        {
            Email = "test@test.com",
            Phone = phone,
            Nickname = "testuser"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _userService.GetUserByPhoneAsync(phone);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(phone, result.Phone);
        Assert.Equal("test@test.com", result.Email);
    }

    // 전화번호로 사용자 조회 실패 (없음)
    [Fact]
    public async Task GetUserByPhoneAsync_ShouldReturnNull_WhenUserDoesNotExist()
    {
        // Arrange
        InitializeContext();
        var phone = "01099999999";

        // Act
        var result = await _userService.GetUserByPhoneAsync(phone);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region 이메일로 유저 찾기 테스트

    // 이메일로 사용자 조회 성공
    [Fact]
    public async Task GetUserByEmailAsync_ShouldReturnUser_WhenUserExists()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var user = new User
        {
            Email = email,
            Phone = "01012345678",
            Nickname = "test"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _userService.GetUserByEmailAsync(email);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(email, result.Email);
        Assert.Equal("test", result.Nickname);
    }

    // 이메일로 사용자 조회 실패
    [Fact]
    public async Task GetUserByEmailAsync_ShouldReturnNull_WhenUserDoesNotExist()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";

        // Act
        var result = await _userService.GetUserByEmailAsync(email);

        // Assert
        Assert.Null(result);
    }

    // 대소문자가 다른 이메일로 조회해도 동일 사용자 반환
    [Fact]
    public async Task GetUserByEmailAsync_ShouldBeCaseInsensitive()
    {
        // Arrange
        InitializeContext();
        var email = "Test@Example.com";
        var user = new User
        {
            Email = email,
            Phone = "01012345678",
            Nickname = "testuser"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _userService.GetUserByEmailAsync("test@example.com");

        // Assert
        Assert.NotNull(result); // 대소문자가 달라도 조회됨
        Assert.Equal(email, result.Email);
    }

    #endregion

    #region 닉네임으로 유저 찾기 테스트

    // 닉네임으로 사용자 조회 성공
    [Fact]
    public async Task GetUserByNicknameAsync_ShouldReturnUser_WhenUserExists()
    {
        // Arrange
        InitializeContext();
        var nickname = "test";
        var user = new User
        {
            Email = "test@test.com",
            Phone = "01012345678",
            Nickname = nickname
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _userService.GetUserByNicknameAsync(nickname);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(nickname, result.Nickname);
        Assert.Equal("test@test.com", result.Email);
    }

    // 닉네임으로 사용자 조회 실패
    [Fact]
    public async Task GetUserByNicknameAsync_ShouldReturnNull_WhenUserDoesNotExist()
    {
        // Arrange
        InitializeContext();
        var nickname = "test";

        // Act
        var result = await _userService.GetUserByNicknameAsync(nickname);

        // Assert
        Assert.Null(result);
    }

    // 대소문자가 다른 닉네임으로 조회하면 찾을 수 없음
    [Fact]
    public async Task GetUserByNicknameAsync_ShouldBeCaseSensitive()
    {
        // Arrange
        InitializeContext();
        var nickname = "Test";
        var user = new User
        {
            Email = "test@test.com",
            Phone = "01012345678",
            Nickname = nickname
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _userService.GetUserByNicknameAsync("test");

        // Assert
        Assert.Null(result); // 대소문자가 다르면 조회 안 됨
    }

    #endregion
}
