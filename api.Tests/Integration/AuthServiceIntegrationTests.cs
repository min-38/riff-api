using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using api.Data;
using api.DTOs.Requests;
using api.Models;
using api.Services;
using api.Exceptions;
using api.Utils;

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
        Assert.NotNull(unverifiedUser.EmailVerificationTokenExpiredAt);
        Assert.True(unverifiedUser.EmailVerificationTokenExpiredAt > DateTime.UtcNow);

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
        Assert.Null(verifiedUser.EmailVerificationTokenExpiredAt);
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
        user.EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(-1); // 만료
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
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(-1), // 만료
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
        Assert.True(newUser.EmailVerificationTokenExpiredAt > DateTime.UtcNow);
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

    #region 비밀번호 재설정 플로우 통합 테스트

    // 성공 - 토큰 저장, 24시간 만료 시간 설정
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Auth")]
    public async Task PasswordResetFlow_SendEmailRequest_Success()
    {
        // Arrange
        InitializeContext();

        var email = "test@test.com";
        var password = "Password123!";
        var nickname = "testuser";

        // 기존 사용자 생성
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword(password),
            Nickname = nickname,
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Mock 설정
        _emailServiceMock.Setup(x => x.SendPasswordResetLinkAsync(email, It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("password_reset_daily")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(1);

        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("password_reset_hourly")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(1);

        // Act - 비밀번호 재설정 요청
        var resetResponse = await _authService.SendPasswordResetEmailAsync(email, null);

        // Assert - 재설정 이메일 발송 확인
        Assert.True(resetResponse.Success);
        Assert.Contains("Password reset email sent successfully", resetResponse.Message);

        // 이메일 발송 확인
        _emailServiceMock.Verify(
            x => x.SendPasswordResetLinkAsync(email, It.IsAny<string>()),
            Times.Once
        );

        // DB에서 재설정 토큰 확인
        var userWithToken = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(userWithToken);
        Assert.NotNull(userWithToken.PasswordResetToken);
        Assert.NotNull(userWithToken.PasswordResetTokenExpiredAt);
        Assert.True(userWithToken.PasswordResetTokenExpiredAt > DateTime.UtcNow);
        Assert.True(userWithToken.PasswordResetTokenExpiredAt <= DateTime.UtcNow.AddHours(25)); // 24시간
    }

    // 실패 - 시간당/일당 제한 초과
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Auth")]
    public async Task PasswordResetFlow_WithRateLimiting_ThrowsException()
    {
        // Arrange
        InitializeContext();

        var email = "test@test.com";
        var password = "Password123!";
        var nickname = "testuser";

        // 사용자 생성
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword(password),
            Nickname = nickname,
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Mock: 시간당 제한 초과
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("password_reset_daily")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(2);

        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("password_reset_hourly")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(4); // 4번째 시도 = 초과

        // Act & Assert - Rate Limit 예외 발생
        var exception = await Assert.ThrowsAsync<RateLimitException>(
            () => _authService.SendPasswordResetEmailAsync(email, null)
        );

        Assert.Contains("Too many password reset attempts", exception.Message);
        Assert.Equal(3600, exception.RemainingSeconds);
    }

    // 성공 - 3번째 시도 시 CAPTCHA 요구 및 통과 후 이메일 발송
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Auth")]
    public async Task PasswordResetFlow_With3rdAttempt_RequiresCaptcha()
    {
        // Arrange
        InitializeContext();

        var email = "test@test.com";
        var password = "Password123!";
        var nickname = "testuser";
        var captchaToken = "valid-captcha-token";

        // 사용자 생성
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword(password),
            Nickname = nickname,
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Mock: 3번째 시도
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("password_reset_daily")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(2);

        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("password_reset_hourly")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(3); // 3번째 시도 = CAPTCHA 필요

        // CAPTCHA 없이 시도 -> 실패
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.SendPasswordResetEmailAsync(email, null)
        );

        Assert.Contains("CAPTCHA verification is required", exception.Message);

        // CAPTCHA와 함께 시도 -> 성공
        _captchaServiceMock.Setup(x => x.VerifyTurnstileTokenAsync(captchaToken))
            .ReturnsAsync(true);

        _emailServiceMock.Setup(x => x.SendPasswordResetLinkAsync(email, It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var response = await _authService.SendPasswordResetEmailAsync(email, captchaToken);

        // Assert - CAPTCHA 통과 후 성공
        Assert.True(response.Success);
        Assert.Contains("Password reset email sent successfully", response.Message);

        // CAPTCHA 검증 호출 확인
        _captchaServiceMock.Verify(
            x => x.VerifyTurnstileTokenAsync(captchaToken),
            Times.Once
        );

        // 이메일 발송 확인
        _emailServiceMock.Verify(
            x => x.SendPasswordResetLinkAsync(email, It.IsAny<string>()),
            Times.Once
        );
    }

    // 성공 - 존재하지 않는 이메일로 요청 시 보안상 성공 응답 반환
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Auth")]
    public async Task PasswordResetFlow_WithNonExistentEmail_ReturnsSuccessWithoutSending()
    {
        // Arrange
        InitializeContext();

        var nonExistentEmail = "nonexistent@test.com";

        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.IsAny<string>(),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(1);

        // Act - 존재하지 않는 이메일로 재설정 요청
        var response = await _authService.SendPasswordResetEmailAsync(nonExistentEmail, null);

        // Assert - 보안: 성공 응답 반환 (이메일 존재 여부 노출 방지)
        Assert.True(response.Success);
        Assert.Contains("Password reset email sent successfully", response.Message);

        // 이메일이 실제로 발송되지 않았는지 확인
        _emailServiceMock.Verify(
            x => x.SendPasswordResetLinkAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );

        // DB에 아무 변경이 없는지 확인
        var users = await _context.Users.ToListAsync();
        Assert.Empty(users);
    }

    // 성공 - 차단된 사용자로 요청 시 보안상 성공 응답 반환
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Auth")]
    public async Task PasswordResetFlow_WithBlockedUser_ReturnsSuccessWithoutSending()
    {
        // Arrange
        InitializeContext();

        var email = "blocked@test.com";
        var password = "Password123!";
        var nickname = "blockeduser";

        // 차단된 사용자 생성
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword(password),
            Nickname = nickname,
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        var blockedUser = new BlockedUser
        {
            UserId = user.Id,
            Reason = "Violation of terms",
            BlockedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        _context.BlockedUsers.Add(blockedUser);
        await _context.SaveChangesAsync();

        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.IsAny<string>(),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(1);

        // Act - 차단된 사용자로 재설정 요청
        var response = await _authService.SendPasswordResetEmailAsync(email, null);

        // Assert - 보안: 성공 응답 반환 (차단 여부 노출 방지)
        Assert.True(response.Success);
        Assert.Contains("Password reset email sent successfully", response.Message);

        // 이메일이 실제로 발송되지 않았는지 확인
        _emailServiceMock.Verify(
            x => x.SendPasswordResetLinkAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );

        // DB에서 토큰이 생성되지 않았는지 확인
        var updatedUser = await _context.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Null(updatedUser.PasswordResetToken);
        Assert.Null(updatedUser.PasswordResetTokenExpiredAt);
    }

    // 성공 - 유효한 토큰으로 검증
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Auth")]
    public async Task PasswordResetFlow_VerifyToken_ValidToken_Success()
    {
        // Arrange
        InitializeContext();

        var email = "test@test.com";
        var password = "Password123!";
        var nickname = "testuser";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword(password),
            Nickname = nickname,
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        string? sentResetToken = null;

        _emailServiceMock.Setup(x => x.SendPasswordResetLinkAsync(email, It.IsAny<string>()))
            .Callback<string, string>((e, token) =>
            {
                sentResetToken = token;
            })
            .Returns(Task.CompletedTask);

        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.IsAny<string>(),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(1);

        // Act
        // 비밀번호 재설정 요청
        await _authService.SendPasswordResetEmailAsync(email, null);

        Assert.NotNull(sentResetToken);

        // 토큰 검증
        var verifyResponse = await _authService.VerifyPasswordResetTokenAsync(sentResetToken!);

        // Assert
        Assert.True(verifyResponse.Success);
        Assert.Contains("Valid reset token", verifyResponse.Message);
        Assert.Equal(email, verifyResponse.Email);
    }

    // 실패 - 만료된 토큰으로 검증 시도
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Auth")]
    public async Task PasswordResetFlow_VerifyToken_ExpiredToken_ThrowsException()
    {
        // Arrange
        InitializeContext();

        var email = "test@test.com";
        var password = "Password123!";
        var nickname = "testuser";
        var resetToken = SecurityHelper.GenerateVerificationToken();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword(password),
            Nickname = nickname,
            Verified = true,
            PasswordResetToken = SecurityHelper.HashToken(resetToken),
            PasswordResetTokenExpiredAt = DateTime.UtcNow.AddHours(-1), // 만료됨
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.VerifyPasswordResetTokenAsync(resetToken)
        );

        Assert.Contains("Invalid or expired reset link", exception.Message);
    }

    // 실패 - 잘못된 토큰으로 검증 시도
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Auth")]
    public async Task PasswordResetFlow_VerifyToken_InvalidToken_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var invalidToken = "invalid-token-12345";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.VerifyPasswordResetTokenAsync(invalidToken)
        );

        Assert.Contains("Invalid or expired reset link", exception.Message);
    }

    // 성공 - 비밀번호 재설정 플로우 전체 테스트
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Auth")]
    public async Task PasswordResetFlow_CompleteFlow_Success()
    {
        // Arrange
        InitializeContext();

        var email = "test@test.com";
        var oldPassword = "OldPassword123!";
        var newPassword = "NewPassword456!";
        var nickname = "testuser";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword(oldPassword),
            Nickname = nickname,
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        string? sentResetToken = null;

        _emailServiceMock.Setup(x => x.SendPasswordResetLinkAsync(email, It.IsAny<string>()))
            .Callback<string, string>((e, token) =>
            {
                sentResetToken = token;
            })
            .Returns(Task.CompletedTask);

        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.IsAny<string>(),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(1);

        // 비밀번호 재설정 요청
        var resetResponse = await _authService.SendPasswordResetEmailAsync(email, null);
        Assert.True(resetResponse.Success);
        Assert.NotNull(sentResetToken);

        // 토큰 검증
        var verifyResponse = await _authService.VerifyPasswordResetTokenAsync(sentResetToken!);
        Assert.True(verifyResponse.Success);
        Assert.Equal(email, verifyResponse.Email);

        // Step 3: 비밀번호 재설정
        var changeResponse = await _authService.ResetPasswordAsync(sentResetToken!, newPassword);
        Assert.True(changeResponse.Success);
        Assert.Contains("Password has been reset successfully", changeResponse.Message);

        // Assert
        // 비밀번호가 변경되었는지 확인
        var updatedUser = await _context.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.True(BCrypt.Net.BCrypt.Verify(newPassword, updatedUser.Password));
        Assert.False(BCrypt.Net.BCrypt.Verify(oldPassword, updatedUser.Password));

        // 토큰이 무효화되었는지 확인
        Assert.Null(updatedUser.PasswordResetToken);
        Assert.Null(updatedUser.PasswordResetTokenExpiredAt);

        // 재설정된 토큰으로 다시 시도하면 실패
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.ResetPasswordAsync(sentResetToken!, "AnotherPassword789!")
        );
        Assert.Contains("Invalid or expired reset link", exception.Message);
    }

    // 실패 - 잘못된 토큰으로 비밀번호 재설정 시도
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Auth")]
    public async Task PasswordResetFlow_ResetPassword_InvalidToken_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var invalidToken = "invalid-token-12345";
        var newPassword = "NewPassword456!";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.ResetPasswordAsync(invalidToken, newPassword)
        );

        Assert.Contains("Invalid or expired reset link", exception.Message);
    }

    // 실패 - 만료된 토큰으로 비밀번호 재설정 시도
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Auth")]
    public async Task PasswordResetFlow_ResetPassword_ExpiredToken_ThrowsException()
    {
        // Arrange
        InitializeContext();

        var email = "test@test.com";
        var password = "Password123!";
        var newPassword = "NewPassword456!";
        var nickname = "testuser";
        var resetToken = SecurityHelper.GenerateVerificationToken();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword(password),
            Nickname = nickname,
            Verified = true,
            PasswordResetToken = SecurityHelper.HashToken(resetToken),
            PasswordResetTokenExpiredAt = DateTime.UtcNow.AddHours(-1), // 만료됨
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.ResetPasswordAsync(resetToken, newPassword)
        );

        Assert.Contains("Invalid or expired reset link", exception.Message);
    }

    // 성공 - 비밀번호 재설정 시 RefreshToken 무효화
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Auth")]
    public async Task PasswordResetFlow_ResetPassword_RevokesAllSessions()
    {
        // Arrange
        InitializeContext();

        var email = "test@test.com";
        var oldPassword = "OldPassword123!";
        var newPassword = "NewPassword456!";
        var nickname = "testuser";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword(oldPassword),
            Nickname = nickname,
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        // 기존 RefreshToken 여러 개 추가
        var refreshToken1 = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = "refresh_token_1",
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = null
        };

        var refreshToken2 = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = "refresh_token_2",
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = null
        };

        var refreshToken3 = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = "refresh_token_3",
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = null
        };

        _context.RefreshTokens.AddRange(refreshToken1, refreshToken2, refreshToken3);
        await _context.SaveChangesAsync();

        string? sentResetToken = null;

        _emailServiceMock.Setup(x => x.SendPasswordResetLinkAsync(email, It.IsAny<string>()))
            .Callback<string, string>((e, token) =>
            {
                sentResetToken = token;
            })
            .Returns(Task.CompletedTask);

        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.IsAny<string>(),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(1);

        // Act
        // 비밀번호 재설정 요청
        await _authService.SendPasswordResetEmailAsync(email, null);
        Assert.NotNull(sentResetToken);

        // 기존 RefreshToken 개수 확인
        var tokensBeforeReset = await _context.RefreshTokens
            .Where(rt => rt.UserId == user.Id)
            .ToListAsync();
        Assert.Equal(3, tokensBeforeReset.Count);

        // 비밀번호 재설정
        await _authService.ResetPasswordAsync(sentResetToken!, newPassword);

        // Assert
        // 모든 RefreshToken이 삭제되었는지 확인
        var tokensAfterReset = await _context.RefreshTokens
            .Where(rt => rt.UserId == user.Id)
            .ToListAsync();
        Assert.Empty(tokensAfterReset);
    }

    // 실패 - 차단된 사용자 비밀번호 재설정 시도
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Auth")]
    public async Task PasswordResetFlow_ResetPassword_BlockedUser_ThrowsException()
    {
        // Arrange
        InitializeContext();

        var email = "blocked@test.com";
        var password = "Password123!";
        var newPassword = "NewPassword456!";
        var nickname = "blockeduser";
        var resetToken = SecurityHelper.GenerateVerificationToken();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword(password),
            Nickname = nickname,
            Verified = true,
            PasswordResetToken = SecurityHelper.HashToken(resetToken),
            PasswordResetTokenExpiredAt = DateTime.UtcNow.AddHours(1),
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        var blockedUser = new BlockedUser
        {
            UserId = user.Id,
            Reason = "Violation of terms",
            BlockedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        _context.BlockedUsers.Add(blockedUser);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.ResetPasswordAsync(resetToken, newPassword)
        );

        Assert.Contains("Your account has been blocked", exception.Message);

        // 비밀번호가 변경되지 않았는지 확인
        var unchangedUser = await _context.Users.FindAsync(user.Id);
        Assert.NotNull(unchangedUser);
        Assert.True(BCrypt.Net.BCrypt.Verify(password, unchangedUser.Password));
        Assert.False(BCrypt.Net.BCrypt.Verify(newPassword, unchangedUser.Password));
    }

    // 성공 - 여러 번 재설정 요청 시 토큰이 갱신되는지 확인
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Auth")]
    public async Task PasswordResetFlow_MultipleRequests_UpdatesToken()
    {
        // Arrange
        InitializeContext();

        var email = "test@test.com";
        var password = "Password123!";
        var nickname = "testuser";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword(password),
            Nickname = nickname,
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        string? firstToken = null;
        string? secondToken = null;

        _emailServiceMock.Setup(x => x.SendPasswordResetLinkAsync(email, It.IsAny<string>()))
            .Callback<string, string>((e, token) =>
            {
                if (firstToken == null)
                    firstToken = token;
                else
                    secondToken = token;
            })
            .Returns(Task.CompletedTask);

        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.IsAny<string>(),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(1);

        // Act
        // 첫 번째 재설정 요청
        await _authService.SendPasswordResetEmailAsync(email, null);
        Assert.NotNull(firstToken);

        //두 번째 재설정 요청 (토큰 갱신)
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.IsAny<string>(),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(2);

        await _authService.SendPasswordResetEmailAsync(email, null);
        Assert.NotNull(secondToken);

        // Assert
        // 두 토큰이 다른지 확인
        Assert.NotEqual(firstToken, secondToken);

        // 첫 번째 토큰은 무효화됨
        var firstTokenException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.VerifyPasswordResetTokenAsync(firstToken!)
        );
        Assert.Contains("Invalid or expired reset link", firstTokenException.Message);

        // 두 번째 토큰은 유효함
        var verifyResponse = await _authService.VerifyPasswordResetTokenAsync(secondToken!);
        Assert.True(verifyResponse.Success);
        Assert.Equal(email, verifyResponse.Email);
    }

    #endregion
}
