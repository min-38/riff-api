using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using api.Data;
using api.DTOs.Requests;
using api.Models;
using api.Services;
using api.Exceptions;

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
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly Mock<ICaptchaService> _captchaServiceMock;
    private ApplicationDbContext _context = null!;
    private AuthService _authService = null!;

    public AuthServiceIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<AuthService>>();
        _configurationMock = new Mock<IConfiguration>();
        _userServiceMock = new Mock<IUserService>();
        _emailServiceMock = new Mock<IEmailService>();
        _tokenServiceMock = new Mock<ITokenService>();
        _redisServiceMock = new Mock<IRedisService>();
        _captchaServiceMock = new Mock<ICaptchaService>();
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
            _tokenServiceMock.Object,
            _redisServiceMock.Object,
            _captchaServiceMock.Object
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

        string? sentVerificationToken = null;

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .Returns<string>(async (e) =>
            {
                return await _context.Users.FirstOrDefaultAsync(u => u.Email == e && u.DeletedAt == null);
            });
        _userServiceMock.Setup(x => x.IsEmailAvailableAsync(email))
            .Returns<string>(async (e) =>
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == e && u.DeletedAt == null);
                return user == null;
            });
        _userServiceMock.Setup(x => x.GetUserByNicknameAsync(nickname))
            .ReturnsAsync((User?)null);
        _userServiceMock.Setup(x => x.GetUserByPhoneAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);
        _emailServiceMock.Setup(x => x.SendVerificationLinkAsync(email, It.IsAny<string>()))
            .Callback<string, string>((e, token) =>
            {
                sentVerificationToken = token;
            })
            .Returns(Task.CompletedTask);

        Environment.SetEnvironmentVariable("JWT_EXPIRATION_MINUTES", "60");

        // 회원가입 - 미인증 계정 생성
        var request = new RegisterRequest
        {
            Email = email,
            Password = password,
            PasswordConfirm = password,
            Nickname = nickname,
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true,
            MarketingAgreed = false
        };

        var registerResponse = await _authService.RegisterAsync(request);

        // Assert - 회원가입 응답 확인
        Assert.NotNull(registerResponse);
        Assert.Contains("check your email", registerResponse.Message);
        Assert.Equal(email, registerResponse.Email);

        // Assert - 미인증 계정 생성 확인
        var unverifiedUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(unverifiedUser);
        Assert.Equal(nickname, unverifiedUser.Nickname);
        Assert.False(string.IsNullOrEmpty(unverifiedUser.Password));
        Assert.False(unverifiedUser.Verified); // 아직 미인증
        Assert.NotNull(unverifiedUser.EmailVerificationToken);
        Assert.NotNull(unverifiedUser.EmailVerificationExpiry);
        Assert.True(unverifiedUser.EmailVerificationExpiry > DateTime.UtcNow);

        // Assert - 이메일 발송 확인
        _emailServiceMock.Verify(
            x => x.SendVerificationLinkAsync(email, It.IsAny<string>()),
            Times.Once
        );
        Assert.NotNull(sentVerificationToken);

        // 이메일 인증 - 토큰 클릭
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("access_token");
        _tokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>()))
            .ReturnsAsync("refresh_token");

        var verifyResponse = await _authService.VerifyEmailByTokenAsync(sentVerificationToken!);

        // Assert - 인증 완료 및 자동 로그인
        Assert.NotNull(verifyResponse);
        Assert.Equal(unverifiedUser.Id, verifyResponse.UserId);
        Assert.Equal(email, verifyResponse.Email);
        Assert.Equal(nickname, verifyResponse.Nickname);
        Assert.Equal("access_token", verifyResponse.Token);
        Assert.Equal("refresh_token", verifyResponse.RefreshToken);
        Assert.NotNull(verifyResponse.ExpiresAt);

        // Assert - 계정 인증 상태 확인
        var verifiedUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(verifiedUser);
        Assert.True(verifiedUser.Verified); // 인증 완료
        Assert.Null(verifiedUser.EmailVerificationToken); // 토큰 무효화
        Assert.Null(verifiedUser.EmailVerificationExpiry);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Auth")]
    public async Task VerifyEmailByToken_WithExpiredToken_ThrowsException() // 만료된 토큰으로 인증 시도
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var password = "Password123!";
        var nickname = "test";

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync((User?)null);
        _userServiceMock.Setup(x => x.IsEmailAvailableAsync(email))
            .ReturnsAsync(true);
        _userServiceMock.Setup(x => x.GetUserByNicknameAsync(nickname))
            .ReturnsAsync((User?)null);
        _emailServiceMock.Setup(x => x.SendVerificationLinkAsync(email, It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // 회원가입
        var request = new RegisterRequest
        {
            Email = email,
            Password = password,
            PasswordConfirm = password,
            Nickname = nickname,
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true,
            MarketingAgreed = false
        };

        await _authService.RegisterAsync(request);

        // 토큰 만료 시키기
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(user);
        user.EmailVerificationExpiry = DateTime.UtcNow.AddHours(-1); // 만료
        await _context.SaveChangesAsync();

        // Act & Assert - 만료된 토큰으로 인증 시도
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.VerifyEmailByTokenAsync(user.EmailVerificationToken!)
        );

        Assert.Equal("Invalid or expired verification link", exception.Message);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Auth")]
    public async Task RegisterAsync_WithExpiredUnverifiedAccount_DeletesAndCreatesNew() // 만료된 미인증 계정이 있을 때 재가입 플로우
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var password = "Password123!";
        var nickname = "test";

        // 만료된 미인증 계정 먼저 생성
        var expiredUser = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword("OldPassword123!"),
            Nickname = "oldnickname",
            Verified = false,
            EmailVerificationToken = "old-token",
            EmailVerificationExpiry = DateTime.UtcNow.AddHours(-1), // 만료
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        _context.Users.Add(expiredUser);
        await _context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .Returns<string>(async (e) =>
            {
                return await _context.Users.FirstOrDefaultAsync(u => u.Email == e && u.DeletedAt == null);
            });
        _userServiceMock.Setup(x => x.IsEmailAvailableAsync(email))
            .Returns<string>(async (e) =>
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == e && u.DeletedAt == null);
                return user == null;
            });
        _userServiceMock.Setup(x => x.GetUserByNicknameAsync(nickname))
            .ReturnsAsync((User?)null);
        _emailServiceMock.Setup(x => x.SendVerificationLinkAsync(email, It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act - 새로운 회원가입
        var request = new RegisterRequest
        {
            Email = email,
            Password = password,
            PasswordConfirm = password,
            Nickname = nickname,
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true,
            MarketingAgreed = false
        };

        var response = await _authService.RegisterAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(email, response.Email);

        // 기존 계정은 삭제되고 새 계정이 생성되었는지 확인
        var users = await _context.Users.Where(u => u.Email == email).ToListAsync();
        Assert.Single(users); // 하나만 있어야 함
        var newUser = users[0];
        Assert.NotEqual(expiredUser.Id, newUser.Id); // 새로운 ID
        Assert.Equal(nickname, newUser.Nickname); // 새로운 닉네임
        Assert.False(newUser.Verified);
        Assert.NotNull(newUser.EmailVerificationToken);
        Assert.True(newUser.EmailVerificationExpiry > DateTime.UtcNow);
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
        Assert.NotNull(refreshToken);

        // RefreshToken을 DB에 추가
        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = refreshToken!,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = null
        };
        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        // 로그아웃
        var logoutResult = await _authService.LogOutAsync(refreshToken!);

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
        Assert.NotNull(refreshToken);

        // RefreshToken을 DB에 추가
        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = refreshToken!,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = null
        };
        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        // 토큰 갱신
        var newRefreshToken = "new_refresh_token";
        _tokenServiceMock.Setup(x => x.RefreshAccessTokenAsync(refreshToken!))
            .ReturnsAsync(("new_access_token", newRefreshToken, user));

        var refreshResponse = await _authService.RefreshAccessTokenAsync(refreshToken!);

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
