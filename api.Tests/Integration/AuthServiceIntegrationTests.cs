using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using api.Data;
using api.DTOs.Requests;
using api.Models;
using api.Services;

namespace api.Tests.Integration;

/*
AuthService 통합 테스트
- 전체 회원가입 플로우 등 여러 단계를 거치는 시나리오 테스트
*/
public class AuthServiceIntegrationTests : IDisposable
{
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private ApplicationDbContext _context = null!;
    private AuthService _authService = null!;

    public AuthServiceIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<AuthService>>();
        _configurationMock = new Mock<IConfiguration>();
        _userServiceMock = new Mock<IUserService>();
        _emailServiceMock = new Mock<IEmailService>();
        _tokenServiceMock = new Mock<ITokenService>();
    }

    private void InitializeContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _authService = new AuthService(
            _context,
            _loggerMock.Object,
            _configurationMock.Object,
            _userServiceMock.Object,
            _emailServiceMock.Object,
            _tokenServiceMock.Object
        );
    }

    public void Dispose()
    {
        _context?.Database.EnsureDeleted();
        _context?.Dispose();
    }

    #region 전체 회원가입 플로우 통합 테스트

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Auth")]
    public async Task FullRegistrationFlow_Success()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var password = "Password123!";
        var nickname = "test";

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .Returns<string>(async (e) =>
            {
                return await _context.Users.FirstOrDefaultAsync(u => u.Email == e);
            });
        _userServiceMock.Setup(x => x.GetUserByNicknameAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);
        _userServiceMock.Setup(x => x.GetUserByPhoneAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);
        _emailServiceMock.Setup(x => x.SendVerificationEmailAsync(email, It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // 이메일 인증 요청
        var sendResult = await _authService.SendVerificationCodeAsync(email);
        Assert.True(sendResult);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(user);
        var verificationCode = user.VerificationCode;
        Assert.NotNull(verificationCode);

        // 이메일 인증 확인
        var sessionToken = await _authService.VerifyEmailCodeAsync(email, verificationCode);
        Assert.NotNull(sessionToken);
        Assert.True(user.Verified);

        // 닉네임 중복 체크
        var nicknameAvailable = await _authService.CheckNicknameAvailabilityAsync(nickname);
        Assert.True(nicknameAvailable);

        // 회원가입 완료
        var request = new RegisterRequest
        {
            Email = email,
            SessionToken = sessionToken,
            Password = password,
            PasswordConfirm = password,
            Nickname = nickname
        };

        var response = await _authService.RegisterAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(email, response.Email);
        Assert.Equal(nickname, response.Nickname);
        Assert.True(response.Verified);
        Assert.Null(response.Token);
        Assert.NotEmpty(user.Password);
        Assert.Null(user.RegistrationSessionToken); // 세션 토큰 삭제됨
    }

    #endregion

    #region 로그인/로그아웃 플로우 통합 테스트

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Auth")]
    public async Task FullAuthenticationFlow_LoginAndLogout_Success()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var password = "Password123!";
        var nickname = "test";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = hashedPassword,
            Nickname = nickname,
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync(user);
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(user))
            .Returns("access_token");
        _tokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(user.Id))
            .ReturnsAsync("refresh_token");

        Environment.SetEnvironmentVariable("JWT_EXPIRATION_MINUTES", "60");

        // 로그인
        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = password
        };

        var loginResponse = await _authService.LogInAsync(loginRequest);

        // Assert
        Assert.NotNull(loginResponse);
        Assert.Equal(user.Id, loginResponse.UserId);
        Assert.Equal(email, loginResponse.Email);
        Assert.Equal(nickname, loginResponse.Nickname);
        Assert.Equal("access_token", loginResponse.Token);
        Assert.Equal("refresh_token", loginResponse.RefreshToken);

        var refreshToken = loginResponse.RefreshToken;

        // RefreshToken을 DB에 추가
        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = refreshToken,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = null
        };
        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        // 로그아웃
        var logoutResult = await _authService.LogOutAsync(refreshToken);

        // Assert
        Assert.True(logoutResult);
        var revokedToken = await _context.RefreshTokens.FindAsync(refreshTokenEntity.Id);
        Assert.NotNull(revokedToken);
        Assert.NotNull(revokedToken.RevokedAt);
    }

    #endregion

    #region 토큰 갱신 플로우 통합 테스트

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Auth")]
    public async Task FullAuthenticationFlow_WithTokenRefresh_Success()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var password = "Password123!";
        var nickname = "test";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = hashedPassword,
            Nickname = nickname,
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync(user);
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(user))
            .Returns("access_token");
        _tokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(user.Id))
            .ReturnsAsync("refresh_token");

        Environment.SetEnvironmentVariable("JWT_EXPIRATION_MINUTES", "60");

        // 로그인
        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = password
        };

        var loginResponse = await _authService.LogInAsync(loginRequest);
        Assert.NotNull(loginResponse);
        var refreshToken = loginResponse.RefreshToken;

        // RefreshToken을 DB에 추가
        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = refreshToken,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = null
        };
        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        // 토큰 갱신
        var newRefreshToken = "new_refresh_token";
        _tokenServiceMock.Setup(x => x.RefreshAccessTokenAsync(refreshToken))
            .ReturnsAsync(("new_access_token", newRefreshToken, user));

        var refreshResponse = await _authService.RefreshAccessTokenAsync(refreshToken);

        // Assert
        Assert.NotNull(refreshResponse);
        Assert.Equal(user.Id, refreshResponse.UserId);
        Assert.Equal("new_access_token", refreshResponse.Token);
        Assert.Equal(newRefreshToken, refreshResponse.RefreshToken);

        // 새 RefreshToken을 DB에 추가
        var newRefreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = newRefreshToken,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = null
        };
        _context.RefreshTokens.Add(newRefreshTokenEntity);
        await _context.SaveChangesAsync();

        // 3. 로그아웃
        var logoutResult = await _authService.LogOutAsync(newRefreshToken);

        // Assert - 로그아웃 성공
        Assert.True(logoutResult);
        var revokedToken = await _context.RefreshTokens.FindAsync(newRefreshTokenEntity.Id);
        Assert.NotNull(revokedToken);
        Assert.NotNull(revokedToken.RevokedAt);
    }

    #endregion
}
