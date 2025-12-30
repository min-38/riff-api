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
using DotNetEnv;

namespace api.Tests.Services;

public class AuthServiceTests : IDisposable
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

    public AuthServiceTests()
    {
        Env.Load();

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

    #region 회원가입 테스트

    // 정상 회원가입
    [Fact]
    public async Task RegisterAsync_ValidRequest_Success()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync((User?)null);
        _userServiceMock.Setup(x => x.GetUserByNicknameAsync("testuser"))
            .ReturnsAsync((User?)null);
        _emailServiceMock.Setup(x => x.SendVerificationLinkAsync(email, It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var request = new RegisterRequest
        {
            Email = email,
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "testuser",
            Phone = "010-1234-5678",
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true,
            MarketingAgreed = false
        };

        // Act
        var response = await _authService.RegisterAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(email, response.Email);
        Assert.Contains("check your email", response.Message);
        Assert.NotNull(response.VerificationToken);
        Assert.NotEmpty(response.VerificationToken);

        // DB에 미인증 User가 생성되었는지 확인
        var createdUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(createdUser);
        Assert.Equal("testuser", createdUser.Nickname);
        Assert.False(createdUser.Verified); // 미인증 상태
        Assert.NotNull(createdUser.EmailVerificationToken);
        Assert.NotNull(createdUser.EmailVerificationTokenExpiredAt);
        Assert.True(createdUser.EmailVerificationTokenExpiredAt > DateTime.UtcNow);

        // 이메일이 발송되었는지 확인
        _emailServiceMock.Verify(x => x.SendVerificationLinkAsync(email, It.IsAny<string>()), Times.Once);

        // Redis에 마지막 전송 시간이 저장되었는지 확인
        _redisServiceMock.Verify(x => x.SetAsync<string>(
            It.Is<string>(k => k.Contains("email_verification_last_sent")),
            It.IsAny<string>(),
            TimeSpan.FromSeconds(60)
        ), Times.Once);
    }

    // 이메일 중복
    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Verified = true,
            Password = "hashedpassword",
            Nickname = "test",
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync(existingUser);

        var request = new RegisterRequest
        {
            Email = email,
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "newuser",
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.RegisterAsync(request)
        );
        Assert.Equal("Email already exists", exception.Message);
    }

    // 만료된 미인증 계정 삭제 후 재가입 성공
    [Fact]
    public async Task RegisterAsync_ExpiredUnverifiedAccount_DeletesAndCreatesNew()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var expiredUser = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Verified = false,
            EmailVerificationToken = "old_token",
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(-1), // 만료됨
            Password = "oldpassword",
            Nickname = "oldnickname",
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _context.Users.Add(expiredUser);
        await _context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync(expiredUser);
        _userServiceMock.Setup(x => x.GetUserByNicknameAsync("newuser"))
            .ReturnsAsync((User?)null);
        _emailServiceMock.Setup(x => x.SendVerificationLinkAsync(email, It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var request = new RegisterRequest
        {
            Email = email,
            Password = "NewPassword123!",
            PasswordConfirm = "NewPassword123!",
            Nickname = "newuser",
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true
        };

        // Act
        var response = await _authService.RegisterAsync(request);

        // Assert
        Assert.NotNull(response);
        var remainingUsers = await _context.Users.Where(u => u.Email == email).ToListAsync();
        Assert.Single(remainingUsers); // 하나만 있어야 함
        Assert.Equal("newuser", remainingUsers[0].Nickname);
    }

    // 닉네임 중복
    [Fact]
    public async Task RegisterAsync_DuplicateNickname_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "other@test.com",
            Nickname = "existingnick",
            Verified = true,
            Password = "hashedpassword",
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);
        _userServiceMock.Setup(x => x.GetUserByNicknameAsync("existingnick"))
            .ReturnsAsync(existingUser);

        var request = new RegisterRequest
        {
            Email = "new@test.com",
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "existingnick",
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.RegisterAsync(request)
        );
        Assert.Equal("Nickname already exists", exception.Message);
    }

    // 약관 미동의
    [Fact]
    public async Task RegisterAsync_TermsNotAgreed_ThrowsException()
    {
        // Arrange
        InitializeContext();

        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "testuser",
            TermsOfServiceAgreed = false, // 미동의
            PrivacyPolicyAgreed = true
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.RegisterAsync(request)
        );
        Assert.Equal("Terms of service agreement is required", exception.Message);
    }

    #endregion

    #region 이메일 인증 (토큰) 테스트

    // 정상 인증
    [Fact]
    public async Task VerifyEmailByTokenAsync_ValidToken_ReturnsAuthResponse()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var token = "valid_token_12345";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = "hashedpassword",
            Nickname = "testuser",
            Verified = false,
            EmailVerificationToken = token,
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(12),
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(user))
            .Returns("jwt_access_token");
        _tokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(user.Id))
            .ReturnsAsync("jwt_refresh_token");

        Environment.SetEnvironmentVariable("JWT_EXPIRATION_MINUTES", "60");

        // Act
        var response = await _authService.VerifyEmailByTokenAsync(token);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(user.Id, response.UserId);
        Assert.Equal(email, response.Email);
        Assert.Equal("testuser", response.Nickname);
        Assert.True(response.Verified);
        Assert.Equal("jwt_access_token", response.Token);
        Assert.Equal("jwt_refresh_token", response.RefreshToken);
        Assert.NotNull(response.ExpiresAt);

        // DB 확인
        var verifiedUser = await _context.Users.FindAsync(user.Id);
        Assert.NotNull(verifiedUser);
        Assert.True(verifiedUser.Verified);
        Assert.Null(verifiedUser.EmailVerificationToken); // 토큰 무효화됨
        Assert.Null(verifiedUser.EmailVerificationTokenExpiredAt);
    }

    // 유효하지 않은 토큰
    [Fact]
    public async Task VerifyEmailByTokenAsync_InvalidToken_ThrowsException()
    {
        // Arrange
        InitializeContext();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.VerifyEmailByTokenAsync("invalid_token")
        );
        Assert.Equal("Invalid or expired verification link", exception.Message);
    }

    // 만료된 토큰
    [Fact]
    public async Task VerifyEmailByTokenAsync_ExpiredToken_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            Password = "hashedpassword",
            Nickname = "testuser",
            Verified = false,
            EmailVerificationToken = "expired_token",
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(-1), // 만료됨
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.VerifyEmailByTokenAsync("expired_token")
        );
        Assert.Equal("Invalid or expired verification link", exception.Message);
    }

    // 이미 인증된 계정
    [Fact]
    public async Task VerifyEmailByTokenAsync_AlreadyVerified_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            Password = "hashedpassword",
            Nickname = "testuser",
            Verified = true, // 이미 인증됨
            EmailVerificationToken = "some_token",
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(12),
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.VerifyEmailByTokenAsync("some_token")
        );
        Assert.Equal("Invalid or expired verification link", exception.Message);
    }

    #endregion

    #region 로그인 테스트

    // 정상 로그인
    [Fact]
    public async Task LogInAsync_ValidCredentials_ReturnsToken()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var password = "Password123!";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = hashedPassword,
            Nickname = "test",
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync(user);
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(user))
            .Returns("jwt_token");
        _tokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(user.Id))
            .ReturnsAsync("refresh_token");

        Environment.SetEnvironmentVariable("JWT_EXPIRATION_MINUTES", "60");

        var request = new LoginRequest
        {
            Email = email,
            Password = password
        };

        // Act
        var response = await _authService.LogInAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(user.Id, response.UserId);
        Assert.Equal(email, response.Email);
        Assert.Equal("test", response.Nickname);
        Assert.Equal("jwt_token", response.Token);
        Assert.Equal("refresh_token", response.RefreshToken);
        Assert.NotNull(response.ExpiresAt);
    }

    // 미인증 계정 로그인 시도 - 첫 시도 (이메일 발송)
    [Fact]
    public async Task LogInAsync_UnverifiedUser_FirstAttempt_SendsEmailAndReturnsCooldown()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var password = "Password123!";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        var verificationToken = "verification_token_12345";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = hashedPassword,
            Nickname = "test",
            Verified = false, // 미인증
            EmailVerificationToken = verificationToken,
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(12),
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync(user);

        // Redis: 마지막 전송 기록 없음
        _redisServiceMock.Setup(x => x.GetAsync<string>(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _emailServiceMock.Setup(x => x.SendVerificationLinkAsync(email, verificationToken))
            .Returns(Task.CompletedTask);

        var request = new LoginRequest
        {
            Email = email,
            Password = password
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnverifiedAccountException>(
            () => _authService.LogInAsync(request)
        );

        Assert.Equal("Email verification required", exception.Message);
        Assert.Equal(verificationToken, exception.VerificationToken);
        Assert.Equal(60, exception.RemainingCooldown); // 방금 발송했으므로 60초

        // 이메일이 발송되었는지 확인
        _emailServiceMock.Verify(x => x.SendVerificationLinkAsync(email, verificationToken), Times.Once);

        // Redis에 마지막 전송 시간 저장되었는지 확인
        _redisServiceMock.Verify(x => x.SetAsync<string>(
            It.Is<string>(k => k.Contains("email_verification_last_sent")),
            It.IsAny<string>(),
            TimeSpan.FromSeconds(60)
        ), Times.Once);
    }

    // 미인증 계정 로그인 시도 - 쿨다운 중 (이메일 발송 안함)
    [Fact]
    public async Task LogInAsync_UnverifiedUser_DuringCooldown_NoEmailSentReturnsCooldown()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var password = "Password123!";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        var verificationToken = "verification_token_12345";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = hashedPassword,
            Nickname = "test",
            Verified = false,
            EmailVerificationToken = verificationToken,
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(12),
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync(user);

        // Redis: 30초 전에 이메일 발송됨
        var lastSentTime = DateTime.UtcNow.AddSeconds(-30);
        _redisServiceMock.Setup(x => x.GetAsync<string>(It.IsAny<string>()))
            .ReturnsAsync(lastSentTime.ToString("O"));

        var request = new LoginRequest
        {
            Email = email,
            Password = password
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnverifiedAccountException>(
            () => _authService.LogInAsync(request)
        );

        Assert.Equal("Email verification required", exception.Message);
        Assert.Equal(verificationToken, exception.VerificationToken);
        Assert.NotNull(exception.RemainingCooldown);
        Assert.True(exception.RemainingCooldown > 20 && exception.RemainingCooldown <= 30); // 약 30초 남음

        // 이메일이 발송되지 않았는지 확인
        _emailServiceMock.Verify(x => x.SendVerificationLinkAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // 미인증 계정 로그인 시도 - 쿨다운 이후 (이메일 재발송)
    [Fact]
    public async Task LogInAsync_UnverifiedUser_AfterCooldown_SendsEmailAgain()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var password = "Password123!";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        var verificationToken = "verification_token_12345";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = hashedPassword,
            Nickname = "test",
            Verified = false,
            EmailVerificationToken = verificationToken,
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(12),
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync(user);

        // Redis: 70초 전에 이메일 발송됨 (쿨다운 지남)
        var lastSentTime = DateTime.UtcNow.AddSeconds(-70);
        _redisServiceMock.Setup(x => x.GetAsync<string>(It.IsAny<string>()))
            .ReturnsAsync(lastSentTime.ToString("O"));

        _emailServiceMock.Setup(x => x.SendVerificationLinkAsync(email, verificationToken))
            .Returns(Task.CompletedTask);

        var request = new LoginRequest
        {
            Email = email,
            Password = password
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnverifiedAccountException>(
            () => _authService.LogInAsync(request)
        );

        Assert.Equal("Email verification required", exception.Message);
        Assert.Equal(verificationToken, exception.VerificationToken);
        Assert.Equal(60, exception.RemainingCooldown); // 방금 발송했으므로 60초

        // 이메일이 발송되었는지 확인
        _emailServiceMock.Verify(x => x.SendVerificationLinkAsync(email, verificationToken), Times.Once);
    }

    // 미인증 계정 로그인 시도 - 토큰 만료 시 새 토큰 생성
    [Fact]
    public async Task LogInAsync_UnverifiedUser_ExpiredToken_GeneratesNewToken()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var password = "Password123!";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = hashedPassword,
            Nickname = "test",
            Verified = false,
            EmailVerificationToken = "old_expired_token",
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(-1), // 만료됨
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync(user);

        _redisServiceMock.Setup(x => x.GetAsync<string>(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        _emailServiceMock.Setup(x => x.SendVerificationLinkAsync(email, It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var request = new LoginRequest
        {
            Email = email,
            Password = password
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnverifiedAccountException>(
            () => _authService.LogInAsync(request)
        );

        // DB에서 새 토큰이 생성되었는지 확인
        var updatedUser = await _context.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.NotEqual("old_expired_token", updatedUser.EmailVerificationToken);
        Assert.NotNull(updatedUser.EmailVerificationTokenExpiredAt);
        Assert.True(updatedUser.EmailVerificationTokenExpiredAt > DateTime.UtcNow);

        // 새 토큰이 예외에 포함되었는지 확인
        Assert.Equal(updatedUser.EmailVerificationToken, exception.VerificationToken);
    }

    // 차단된 계정 로그인 시도 (영구 차단)
    [Fact]
    public async Task LogInAsync_BlockedUser_Permanent_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var email = "blocked@test.com";
        var password = "Password123!";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = hashedPassword,
            Nickname = "test",
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var blockedUser = new BlockedUser
        {
            UserId = user.Id,
            Email = email,
            Reason = "Violation of terms",
            ExpiresAt = null, // 영구 차단
            BlockedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        _context.BlockedUsers.Add(blockedUser);
        await _context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync(user);

        var request = new LoginRequest
        {
            Email = email,
            Password = password
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.LogInAsync(request)
        );

        Assert.Contains("Violation of terms", exception.Message);
    }

    // 차단된 계정 로그인 시도 (일시적 차단)
    [Fact]
    public async Task LogInAsync_BlockedUser_Temporary_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var email = "blocked@test.com";
        var password = "Password123!";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = hashedPassword,
            Nickname = "test",
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var expiresAt = DateTime.UtcNow.AddDays(7);
        var blockedUser = new BlockedUser
        {
            UserId = user.Id,
            Email = email,
            Reason = "Spam activity",
            ExpiresAt = expiresAt, // 일시적 차단
            BlockedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        _context.BlockedUsers.Add(blockedUser);
        await _context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync(user);

        var request = new LoginRequest
        {
            Email = email,
            Password = password
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.LogInAsync(request)
        );

        Assert.Contains("Spam activity", exception.Message);
        Assert.Contains("Access will be restored at", exception.Message);
    }

    // 차단이 만료된 계정 로그인 성공
    [Fact]
    public async Task LogInAsync_ExpiredBlockedUser_Success()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var password = "Password123!";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = hashedPassword,
            Nickname = "test",
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // 차단이 만료됨
        var blockedUser = new BlockedUser
        {
            UserId = user.Id,
            Email = email,
            Reason = "Old violation",
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // 이미 만료됨
            BlockedAt = DateTime.UtcNow.AddDays(-10)
        };

        _context.Users.Add(user);
        _context.BlockedUsers.Add(blockedUser);
        await _context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync(user);

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(user))
            .Returns("jwt_token");
        _tokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(user.Id))
            .ReturnsAsync("refresh_token");

        Environment.SetEnvironmentVariable("JWT_EXPIRATION_MINUTES", "60");

        var request = new LoginRequest
        {
            Email = email,
            Password = password
        };

        // Act
        var response = await _authService.LogInAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(user.Id, response.UserId);
        Assert.Equal(email, response.Email);
    }

    // 잘못된 비밀번호
    [Fact]
    public async Task LogInAsync_InvalidPassword_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword("CorrectPassword123!");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = hashedPassword,
            Nickname = "test",
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync(user);

        var request = new LoginRequest
        {
            Email = email,
            Password = "WrongPassword123!"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.LogInAsync(request)
        );
        Assert.Equal("Invalid credentials", exception.Message);
    }

    #endregion

    #region 로그아웃 테스트

    // 정상 로그아웃
    [Fact]
    public async Task LogOutAsync_ValidRefreshToken_Success()
    {
        // Arrange
        InitializeContext();
        var userId = Guid.NewGuid();
        var refreshToken = "valid_refresh_token";

        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = refreshToken,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = null
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        // Act
        var result = await _authService.LogOutAsync(refreshToken);

        // Assert
        Assert.True(result);
        var revokedToken = await _context.RefreshTokens.FindAsync(refreshTokenEntity.Id);
        Assert.NotNull(revokedToken);
        Assert.NotNull(revokedToken.RevokedAt);
    }

    #endregion

    #region 인증 정보 조회 테스트

    // 정상 조회 - 쿨다운 없음
    [Fact]
    public async Task GetVerificationInfoAsync_ValidToken_NoCooldown_ReturnsInfo()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var token = "valid_verification_token";
        var createdAt = DateTime.UtcNow.AddMinutes(-10);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = "hashedpassword",
            Nickname = "testuser",
            Verified = false,
            EmailVerificationToken = token,
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(12),
            Rating = 0.0,
            CreatedAt = createdAt,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Redis: 마지막 전송 기록 없음
        _redisServiceMock.Setup(x => x.GetAsync<string>(It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        // Act
        var response = await _authService.GetVerificationInfoAsync(token);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(email, response.Email);
        Assert.Equal(createdAt, response.SentAt);
        Assert.Null(response.RemainingCooldown); // 쿨다운 없음
    }

    // 정상 조회 - 쿨다운 중
    [Fact]
    public async Task GetVerificationInfoAsync_ValidToken_DuringCooldown_ReturnsInfoWithCooldown()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var token = "valid_verification_token";
        var createdAt = DateTime.UtcNow.AddMinutes(-10);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = "hashedpassword",
            Nickname = "testuser",
            Verified = false,
            EmailVerificationToken = token,
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(12),
            Rating = 0.0,
            CreatedAt = createdAt,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Redis: 45초 전에 이메일 발송됨
        var lastSentTime = DateTime.UtcNow.AddSeconds(-45);
        _redisServiceMock.Setup(x => x.GetAsync<string>(It.IsAny<string>()))
            .ReturnsAsync(lastSentTime.ToString("O"));

        // Act
        var response = await _authService.GetVerificationInfoAsync(token);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(email, response.Email);
        Assert.Equal(createdAt, response.SentAt);
        Assert.NotNull(response.RemainingCooldown);
        Assert.True(response.RemainingCooldown > 10 && response.RemainingCooldown <= 15); // 약 15초 남음
    }

    // 정상 조회 - 쿨다운 만료
    [Fact]
    public async Task GetVerificationInfoAsync_ValidToken_ExpiredCooldown_ReturnsInfoWithoutCooldown()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var token = "valid_verification_token";
        var createdAt = DateTime.UtcNow.AddMinutes(-10);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = "hashedpassword",
            Nickname = "testuser",
            Verified = false,
            EmailVerificationToken = token,
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(12),
            Rating = 0.0,
            CreatedAt = createdAt,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Redis: 70초 전에 이메일 발송됨 (쿨다운 만료)
        var lastSentTime = DateTime.UtcNow.AddSeconds(-70);
        _redisServiceMock.Setup(x => x.GetAsync<string>(It.IsAny<string>()))
            .ReturnsAsync(lastSentTime.ToString("O"));

        // Act
        var response = await _authService.GetVerificationInfoAsync(token);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(email, response.Email);
        Assert.Equal(createdAt, response.SentAt);
        Assert.Null(response.RemainingCooldown); // 쿨다운 만료되어 null
    }

    // 유효하지 않은 토큰
    [Fact]
    public async Task GetVerificationInfoAsync_InvalidToken_ThrowsException()
    {
        // Arrange
        InitializeContext();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.GetVerificationInfoAsync("invalid_token")
        );
        Assert.Equal("Invalid or expired verification token", exception.Message);
    }

    // 만료된 토큰
    [Fact]
    public async Task GetVerificationInfoAsync_ExpiredToken_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            Password = "hashedpassword",
            Nickname = "testuser",
            Verified = false,
            EmailVerificationToken = "expired_token",
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(-1), // 만료됨
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.GetVerificationInfoAsync("expired_token")
        );
        Assert.Equal("Invalid or expired verification token", exception.Message);
    }

    // 이미 인증된 계정
    [Fact]
    public async Task GetVerificationInfoAsync_AlreadyVerified_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            Password = "hashedpassword",
            Nickname = "testuser",
            Verified = true, // 이미 인증됨
            EmailVerificationToken = "some_token",
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(12),
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.GetVerificationInfoAsync("some_token")
        );
        Assert.Equal("Invalid or expired verification token", exception.Message);
    }

    #endregion

    #region 인증 이메일 재전송 테스트

    // 정상 재전송
    [Fact]
    public async Task ResendVerificationEmailAsync_ValidToken_Success()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var oldToken = "old_token_12345";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = "hashedpassword",
            Nickname = "testuser",
            Verified = false,
            EmailVerificationToken = oldToken,
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(12),
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _redisServiceMock.Setup(x => x.IncrementAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(1); // 첫 번째 시도
        _emailServiceMock.Setup(x => x.SendVerificationLinkAsync(email, It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await _authService.ResendVerificationEmailAsync(oldToken);

        // Assert
        Assert.NotNull(response);
        Assert.Contains("resent", response.Message);

        // DB에서 토큰과 만료 시간 확인
        var updatedUser = await _context.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(oldToken, updatedUser.EmailVerificationToken); // 토큰은 변경되지 않음
        Assert.NotNull(updatedUser.EmailVerificationToken);
        Assert.NotNull(updatedUser.EmailVerificationTokenExpiredAt);
        Assert.True(updatedUser.EmailVerificationTokenExpiredAt > DateTime.UtcNow);

        // Redis rate limiting이 호출되었는지 확인 (일일, 시간당)
        _redisServiceMock.Verify(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("resend_email_daily")),
            TimeSpan.FromHours(24)
        ), Times.Once);
        _redisServiceMock.Verify(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("resend_email_hourly")),
            TimeSpan.FromHours(1)
        ), Times.Once);
        // 이메일이 발송되었는지 확인
        _emailServiceMock.Verify(x => x.SendVerificationLinkAsync(email, It.IsAny<string>()), Times.Once);

        // Redis에 마지막 전송 시간이 저장되었는지 확인
        _redisServiceMock.Verify(x => x.SetAsync<string>(
            It.Is<string>(k => k.Contains("email_verification_last_sent")),
            It.IsAny<string>(),
            TimeSpan.FromSeconds(60)
        ), Times.Once);
    }

    // Rate limit 초과
    [Fact]
    public async Task ResendVerificationEmailAsync_ExceedsRateLimit_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var token = "valid_token_12345";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = "hashedpassword",
            Nickname = "testuser",
            Verified = false,
            EmailVerificationToken = token,
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(12),
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _redisServiceMock.Setup(x => x.IncrementAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync(6); // 6번째 시도 (제한 초과)

        // Act & Assert
        var exception = await Assert.ThrowsAsync<RateLimitException>(
            () => _authService.ResendVerificationEmailAsync(token)
        );
        Assert.Contains("Too many resend attempts", exception.Message);

        // 이메일이 발송되지 않았는지 확인
        _emailServiceMock.Verify(x => x.SendVerificationLinkAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // 유효하지 않은 토큰 (토큰 검증 실패 시 Redis 호출 안됨)
    [Fact]
    public async Task ResendVerificationEmailAsync_InvalidToken_ThrowsException()
    {
        // Arrange
        InitializeContext();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.ResendVerificationEmailAsync("invalid_token")
        );
        Assert.Equal("Invalid or expired verification token", exception.Message);

        // Redis가 호출되지 않았는지 확인 (토큰 검증 전에 실패)
        _redisServiceMock.Verify(x => x.IncrementAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    // 만료된 토큰 (토큰 검증 실패 시 Redis 호출 안됨)
    [Fact]
    public async Task ResendVerificationEmailAsync_ExpiredToken_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            Password = "hashedpassword",
            Nickname = "testuser",
            Verified = false,
            EmailVerificationToken = "expired_token",
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(-1), // 만료됨
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.ResendVerificationEmailAsync("expired_token")
        );
        Assert.Equal("Invalid or expired verification token", exception.Message);

        // Redis가 호출되지 않았는지 확인 (토큰 검증 전에 실패)
        _redisServiceMock.Verify(x => x.IncrementAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    // 이미 인증된 계정 (토큰 검증 실패 시 Redis 호출 안됨)
    [Fact]
    public async Task ResendVerificationEmailAsync_AlreadyVerified_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            Password = "hashedpassword",
            Nickname = "testuser",
            Verified = true, // 이미 인증됨
            EmailVerificationToken = "some_token",
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(12),
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.ResendVerificationEmailAsync("some_token")
        );
        Assert.Equal("Invalid or expired verification token", exception.Message);

        // Redis가 호출되지 않았는지 확인 (토큰 검증 전에 실패)
        _redisServiceMock.Verify(x => x.IncrementAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    #endregion

    #region 토큰 갱신 테스트

    // 정상 갱신
    [Fact]
    public async Task RefreshAccessTokenAsync_ValidToken_ReturnsNewTokens()
    {
        // Arrange
        InitializeContext();
        var userId = Guid.NewGuid();
        var refreshToken = "valid_refresh_token";

        var user = new User
        {
            Id = userId,
            Email = "test@test.com",
            Password = "hashedpassword",
            Nickname = "test",
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _tokenServiceMock
            .Setup(x => x.RefreshAccessTokenAsync(refreshToken))
            .ReturnsAsync(("new_access_token", "new_refresh_token", user));

        Environment.SetEnvironmentVariable("JWT_EXPIRATION_MINUTES", "60");

        // Act
        var response = await _authService.RefreshAccessTokenAsync(refreshToken);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(userId, response.UserId);
        Assert.Equal(user.Email, response.Email);
        Assert.Equal(user.Nickname, response.Nickname);
        Assert.Equal("new_access_token", response.Token);
        Assert.Equal("new_refresh_token", response.RefreshToken);
        Assert.NotNull(response.ExpiresAt);
        Assert.True(response.Verified);
    }

    #endregion

    #region ResendVerificationEmailAsync - CAPTCHA Tests

    [Fact]
    public async Task ResendVerificationEmailAsync_LessThan3Attempts_NoCaptchaRequired()
    {
        // Arrange
        InitializeContext();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Password = "hashedpassword",
            Nickname = "testuser",
            Verified = false,
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Mock Redis: 2번째 시도 (CAPTCHA 불필요)
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("resend_email_daily")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(2);

        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("resend_email_hourly")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(2);

        _emailServiceMock.Setup(x => x.SendVerificationLinkAsync(
            It.IsAny<string>(),
            It.IsAny<string>()
        )).Returns(Task.CompletedTask);

        // Act
        var response = await _authService.ResendVerificationEmailAsync("valid-token", null);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Verification email has been resent. Please check your email.", response.Message);
        _emailServiceMock.Verify(x => x.SendVerificationLinkAsync(
            user.Email,
            It.IsAny<string>()
        ), Times.Once);
        // CAPTCHA가 필요 없으므로 호출되지 않아야 함
        _captchaServiceMock.Verify(x => x.VerifyTurnstileTokenAsync(
            It.IsAny<string>(),
            It.IsAny<string>()
        ), Times.Never);
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_3rdAttempt_NoCaptchaToken_ThrowsException()
    {
        // Arrange
        InitializeContext();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Password = "hashedpassword",
            Nickname = "testuser",
            Verified = false,
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Mock Redis: 3번째 시도 (CAPTCHA 필요)
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("resend_email_daily")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(3);

        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("resend_email_hourly")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(3);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.ResendVerificationEmailAsync("valid-token", null)
        );

        Assert.Contains("CAPTCHA verification is required", exception.Message);
        _emailServiceMock.Verify(x => x.SendVerificationLinkAsync(
            It.IsAny<string>(),
            It.IsAny<string>()
        ), Times.Never);
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_3rdAttempt_ValidCaptcha_Success()
    {
        // Arrange
        InitializeContext();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Password = "hashedpassword",
            Nickname = "testuser",
            Verified = false,
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Mock Redis: 3번째 시도
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("resend_email_daily")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(3);

        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("resend_email_hourly")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(3);

        // Mock CAPTCHA: 유효한 토큰
        _captchaServiceMock.Setup(x => x.VerifyTurnstileTokenAsync(
            "valid-captcha-token",
            It.IsAny<string>()
        )).ReturnsAsync(true);

        _emailServiceMock.Setup(x => x.SendVerificationLinkAsync(
            It.IsAny<string>(),
            It.IsAny<string>()
        )).Returns(Task.CompletedTask);

        // Act
        var response = await _authService.ResendVerificationEmailAsync("valid-token", "valid-captcha-token");

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Verification email has been resent. Please check your email.", response.Message);
        _captchaServiceMock.Verify(x => x.VerifyTurnstileTokenAsync(
            "valid-captcha-token",
            It.IsAny<string>()
        ), Times.Once);
        _emailServiceMock.Verify(x => x.SendVerificationLinkAsync(
            user.Email,
            It.IsAny<string>()
        ), Times.Once);
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_3rdAttempt_InvalidCaptcha_ThrowsException()
    {
        // Arrange
        InitializeContext();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Password = "hashedpassword",
            Nickname = "testuser",
            Verified = false,
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Mock Redis: 3번째 시도
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("resend_email_daily")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(3);

        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("resend_email_hourly")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(3);

        // Mock CAPTCHA: 유효하지 않은 토큰
        _captchaServiceMock.Setup(x => x.VerifyTurnstileTokenAsync(
            "invalid-captcha-token",
            It.IsAny<string>()
        )).ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.ResendVerificationEmailAsync("valid-token", "invalid-captcha-token")
        );

        Assert.Contains("CAPTCHA verification failed", exception.Message);
        _captchaServiceMock.Verify(x => x.VerifyTurnstileTokenAsync(
            "invalid-captcha-token",
            It.IsAny<string>()
        ), Times.Once);
        _emailServiceMock.Verify(x => x.SendVerificationLinkAsync(
            It.IsAny<string>(),
            It.IsAny<string>()
        ), Times.Never);
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_HourlyRateLimitExceeded_ThrowsRateLimitException()
    {
        // Arrange
        InitializeContext();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Password = "hashedpassword",
            Nickname = "testuser",
            Verified = false,
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Mock Redis: 일일 제한 통과, 시간당 제한 초과
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("resend_email_daily")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(5);

        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("resend_email_hourly")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(6); // 5번 초과

        // Act & Assert
        var exception = await Assert.ThrowsAsync<RateLimitException>(
            () => _authService.ResendVerificationEmailAsync("valid-token", null)
        );

        Assert.Contains("Too many resend attempts", exception.Message);
        Assert.Contains("1 hour", exception.Message);
        Assert.Equal(3600, exception.RemainingSeconds);
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_DailyRateLimitExceeded_ThrowsRateLimitException()
    {
        // Arrange
        InitializeContext();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Password = "hashedpassword",
            Nickname = "testuser",
            Verified = false,
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiredAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Mock Redis: 일일 제한 초과
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("resend_email_daily")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(16); // 15번 초과

        // Act & Assert
        var exception = await Assert.ThrowsAsync<RateLimitException>(
            () => _authService.ResendVerificationEmailAsync("valid-token", null)
        );

        Assert.Contains("Too many resend attempts today", exception.Message);
        Assert.Contains("tomorrow", exception.Message);
        Assert.Equal(86400, exception.RemainingSeconds);
    }

    #endregion

    #region 비밀번호 재설정 요청 검증 테스트

    // 정상 - 사용자 존재, 토큰 생성 및 이메일 발송
    [Fact]
    public async Task SendPasswordResetEmailAsync_ValidEmail_UserExists_Success()
    {
        // Arrange
        InitializeContext();

        string email = "test@test.com";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = "hashedpassword",
            Nickname = "testuser",
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Mock Redis -> 일일 제한 통과
        _redisServiceMock
            .Setup(x => x.IncrementAsync(
                It.Is<string>(k => k.Contains("password_reset_daily")),
                It.IsAny<TimeSpan>()
            ))
            .ReturnsAsync(1);

        // Mock Redis -> 시간당 제한 통과
        _redisServiceMock
            .Setup(x => x.IncrementAsync(        
                It.Is<string>(k => k.Contains("password_reset_hourly")),
                It.IsAny<TimeSpan>()
            ))
            .ReturnsAsync(1);

        // Mock 이메일 발송 설정
        _emailServiceMock
            .Setup(x => x.SendPasswordResetLinkAsync(
                email, // 첫 번째 매개변수 -> 정확히 "test@test.com" 값과 일치해야 함
                It.IsAny<string>() // 두 번째 매개변수(resetToken) -> 어떤 문자열 값이든 허용
            ))
            .Returns(Task.CompletedTask); // 이 메서드가 호출되면 성공적으로 완료된 Task를 반환

        // Act
        var response = await _authService.SendPasswordResetEmailAsync(email, null);

        // Assert
        Assert.True(response.Success);
        Assert.Contains("Password reset email sent successfully.", response.Message);

        // DB에서 토큰이 저장되었는지 확인
        var updatedUser = await _context.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.NotNull(updatedUser.PasswordResetToken);
        Assert.NotNull(updatedUser.PasswordResetTokenExpiredAt);
        Assert.True(updatedUser.PasswordResetTokenExpiredAt > DateTime.UtcNow);
        Assert.True(updatedUser.PasswordResetTokenExpiredAt < DateTime.UtcNow.AddHours(25)); // 24시간

        // 이메일이 발송되었는지 확인
        _emailServiceMock.Verify(x => x.SendPasswordResetLinkAsync(
            email,
            It.IsAny<string>()
        ), Times.Once);
        
        // Redis에 마지막 전송 시간이 저장되었는지 확인
        _redisServiceMock.Verify(x => x.SetAsync<string>(
            It.Is<string>(k => k.Contains("password_reset_last_sent")),
            It.IsAny<string>(),
            TimeSpan.FromSeconds(60)
        ), Times.Once);
    }

    // 정상 - 사용자 미존재(사실은 실패지만, 보안상 성공으로 응답) -> 이메일 미발송
    [Fact]
    public async Task SendPasswordResetEmailAsync_ValidEmail_UserNotExists_ReturnsSuccessWithoutSending()
    {
        // Arrange
        InitializeContext();

        string email = "test@test.com";

        // Mock Redis -> 일일 제한 통과
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("password_reset_daily")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(1);

        // Mock Redis -> 시간당 제한 통과
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("password_reset_hourly")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(1);

        // Act
        var response = await _authService.SendPasswordResetEmailAsync(email, null);

        // Assert
        Assert.True(response.Success);
        Assert.Contains("Password reset email sent successfully.", response.Message);

        // DB에 사용자가 생성되지 않았는지 확인
        var createdUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.Null(createdUser);

        // 이메일이 발송되지 않았는지 확인
        _emailServiceMock.Verify(x => x.SendPasswordResetLinkAsync(
            It.IsAny<string>(),
            It.IsAny<string>()
        ), Times.Never);

        // Redis에 마지막 전송 시간을 저장
        _redisServiceMock.Verify(x => x.SetAsync<string>(
            It.Is<string>(k => k.Contains("password_reset_last_sent")),
            It.IsAny<string>(),
            TimeSpan.FromSeconds(60)
        ), Times.Once); 
    }

    // 실패 - Rate Limit - 시간당 제한 초과 (3회)
    [Fact]
    public async Task SendPasswordResetEmailAsync_HourlyRateLimitExceeded_ThrowsRateLimitException()
    {
        // Arrange
        InitializeContext();

        string email = "test@test.com";

        // Mock Redis: 일일 제한 통과
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("password_reset_daily")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(2);

        // Mock Redis: 시간당 제한 초과 (4번째 시도 = 3 초과)
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("password_reset_hourly")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(4); // hourlyAttemptCount > 3이면 RateLimitException 발생

        // Act & Assert
        var exception = await Assert.ThrowsAsync<RateLimitException>(
            async () => await _authService.SendPasswordResetEmailAsync(email, null)
        );

        // 예외 메시지 및 속성 확인
        Assert.Contains("Too many password reset attempts. Please try again in 1 hour.", exception.Message);
        Assert.Equal(3600, exception.RemainingSeconds); // 1시간 = 3600초
    }

    // 실패 - Rate Limit - 일일 제한 초과 (5회)
    [Fact]
    public async Task SendPasswordResetEmailAsync_DailyRateLimitExceeded_ThrowsRateLimitException()
    {
        // Arrange
        InitializeContext();

        string email = "test@test.com";

        // Mock Redis -> 일일 제한 통과
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("password_reset_daily")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(6);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<RateLimitException>(
            async () => await _authService.SendPasswordResetEmailAsync(email, null)
        );

        // 예외 메시지 및 속성 확인
        Assert.Contains("Too many password reset attempts. Please try again in 24 hours.", exception.Message);
        Assert.Equal(86400, exception.RemainingSeconds); // 24시간 = 86400초
    }

    // 실패 - 3번째 시도, CAPTCHA 토큰 없음
    [Fact]
    public async Task SendPasswordResetEmailAsync_3rdAttempt_NoCaptchaToken_ThrowsException()
    {
        // Arrange
        InitializeContext();

        string email = "test@test.com";

        // Mock Redis: 일일 제한 통과
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("password_reset_daily")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(2);

        // Mock Redis -> 3번째 시도
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("password_reset_hourly")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(3);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _authService.SendPasswordResetEmailAsync(email, null)
        );

        // 예외 메시지 확인
        Assert.Contains("CAPTCHA verification is required", exception.Message);
    }

    // 성공 - 3번째 시도, 유효한 CAPTCHA 토큰
    [Fact]
    public async Task SendPasswordResetEmailAsync_3rdAttempt_ValidCaptcha_Success()
    {
        // Arrange
        InitializeContext();

        string email = "test@test.com";
        string captchaToken = "valid-captcha-token";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = "hashedpassword",
            Nickname = "testuser",
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Mock Redis -> 일일 제한 통과
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("password_reset_daily")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(2);

        // Mock Redis -> 3번째 시도
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("password_reset_hourly")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(3);

        // Mock CAPTCHA -> 유효한 토큰
        _captchaServiceMock.Setup(x => x.VerifyTurnstileTokenAsync(captchaToken))
            .ReturnsAsync(true);

        // Mock 이메일 발송
        _emailServiceMock.Setup(x => x.SendPasswordResetLinkAsync(
            email,
            It.IsAny<string>()
        )).Returns(Task.CompletedTask);

        // Act
        var response = await _authService.SendPasswordResetEmailAsync(email, captchaToken);

        // Assert
        Assert.True(response.Success);
        Assert.Contains("Password reset email sent successfully.", response.Message);

        // CAPTCHA 검증이 호출되었는지 확인
        _captchaServiceMock.Verify(x => x.VerifyTurnstileTokenAsync(captchaToken), Times.Once);

        // 이메일이 발송되었는지 확인
        _emailServiceMock.Verify(x => x.SendPasswordResetLinkAsync(
            email,
            It.IsAny<string>()
        ), Times.Once);
    }

    // 실패 - 3번째 시도, expired CAPTCHA 토큰
    [Fact]
    public async Task SendPasswordResetEmailAsync_3rdAttempt_InvalidCaptcha_ThrowsException()
    {
        // Arrange
        InitializeContext();

        string email = "test@test.com";
        string captchaToken = "invalid-captcha-token";

        // Mock Redis -> 일일 제한 통과
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("password_reset_daily")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(2);

        // Mock Redis -> 3번째 시도
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("password_reset_hourly")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(3);

        // Mock CAPTCHA -> 유효하지 않은 토큰
        _captchaServiceMock.Setup(x => x.VerifyTurnstileTokenAsync(captchaToken))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _authService.SendPasswordResetEmailAsync(email, captchaToken)
        );

        // 예외 메시지 확인
        Assert.Contains("CAPTCHA verification failed", exception.Message);

        // CAPTCHA 검증이 호출되었는지 확인
        _captchaServiceMock.Verify(x => x.VerifyTurnstileTokenAsync(captchaToken), Times.Once);
    }

    // 성공 - 차단된 사용자(사실 실패) -> 비밀번호 재설정 요청 시 성공 응답 (차단 정보 노출 방지)
    [Fact]
    public async Task SendPasswordResetEmailAsync_BlockedUser_ReturnsSuccessWithoutSending()
    {
        // Arrange
        InitializeContext();

        string email = "blocked@test.com";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Password = "hashedpassword",
            Nickname = "blockeduser",
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        // 차단된 사용자 추가
        var blockedUser = new BlockedUser
        {
            UserId = user.Id,
            Reason = "Violation of terms",
            BlockedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };
        _context.BlockedUsers.Add(blockedUser);
        await _context.SaveChangesAsync();

        // Mock Redis -> 일일 제한 통과
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("password_reset_daily")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(1);

        // Mock Redis -> 시간당 제한 통과
        _redisServiceMock.Setup(x => x.IncrementAsync(
            It.Is<string>(k => k.Contains("password_reset_hourly")),
            It.IsAny<TimeSpan>()
        )).ReturnsAsync(1);

        // Act
        var response = await _authService.SendPasswordResetEmailAsync(email, null);

        // Assert
        Assert.True(response.Success);
        Assert.Contains("Password reset email sent successfully.", response.Message);

        // 차단된 사용자이므로 이메일이 발송되지 않았는지 확인
        _emailServiceMock.Verify(x => x.SendPasswordResetLinkAsync(
            It.IsAny<string>(),
            It.IsAny<string>()
        ), Times.Never);

        // DB의 토큰이 업데이트되지 않았는지 확인
        var updatedUser = await _context.Users.FindAsync(user.Id);
        Assert.Null(updatedUser!.PasswordResetToken);
    }

    #endregion

    #region 패스워드 재설정 토큰 검증 테스트

    // 성공 - 유효한 토큰
    [Fact]
    public async Task VerifyPasswordResetTokenAsync_ValidToken_Success()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var token = "valid-reset-token";

        var user = new User
        {
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword("OldPassword123!"),
            Nickname = "testuser",
            Verified = true,
            EmailVerificationToken = null,
            PasswordResetToken = SecurityHelper.HashToken(token), // 토큰 해싱
            PasswordResetTokenExpiredAt = DateTime.UtcNow.AddHours(1), // 1시간 후 만료
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var response = await _authService.VerifyPasswordResetTokenAsync(token);

        // Assert
        Assert.True(response.Success);
        Assert.Equal(email, response.Email);
        Assert.Contains("Valid reset token", response.Message);
    }

    // 실패 - 유효하지 않은 토큰
    [Fact]
    public async Task VerifyPasswordResetTokenAsync_InvalidToken_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var invalidToken = "invalid-token";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.VerifyPasswordResetTokenAsync(invalidToken)
        );

        Assert.Contains("Invalid or expired reset link", exception.Message);
    }

    // 실패 - 만료된 토큰
    [Fact]
    public async Task VerifyPasswordResetTokenAsync_ExpiredToken_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var token = "expired-reset-token";

        var user = new User
        {
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword("OldPassword123!"),
            Nickname = "testuser",
            Verified = true,
            EmailVerificationToken = null,
            PasswordResetToken = SecurityHelper.HashToken(token), // 토큰 해싱
            PasswordResetTokenExpiredAt = DateTime.UtcNow.AddHours(-1), // 1시간 전 만료
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.VerifyPasswordResetTokenAsync(token)
        );

        Assert.Contains("Invalid or expired reset link", exception.Message);
    }

    #endregion

    #region 패스워드 변경(재설정) 테스트

    // 성공 - 유효한 토큰, 패스워드
    [Fact]
    public async Task ResetPasswordAsync_ValidToken_Success()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var token = "valid-reset-token";
        var newPassword = "NewPassword123!";

        var user = new User
        {
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword("OldPassword123!"),
            Nickname = "testuser",
            Verified = true,
            EmailVerificationToken = null,
            PasswordResetToken = SecurityHelper.HashToken(token), // 토큰 해싱
            PasswordResetTokenExpiredAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var oldPasswordHash = user.Password;

        // Act
        var response = await _authService.ResetPasswordAsync(token, newPassword);

        // Assert
        Assert.True(response.Success);
        Assert.Contains("Password has been reset successfully", response.Message);

        // DB 확인
        var updatedUser = await _context.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.NotEqual(oldPasswordHash, updatedUser.Password); // 비밀번호 변경됨
        Assert.True(BCrypt.Net.BCrypt.Verify(newPassword, updatedUser.Password)); // 새 비밀번호 검증
        Assert.Null(updatedUser.PasswordResetToken); // 토큰 무효화
        Assert.Null(updatedUser.PasswordResetTokenExpiredAt); // 만료 시간 제거
    }

    // 실패 - 유효하지 않은 토큰
    [Fact]
    public async Task ResetPasswordAsync_InvalidToken_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var invalidToken = "invalid-token";
        var newPassword = "NewPassword123!";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.ResetPasswordAsync(invalidToken, newPassword)
        );

        Assert.Contains("Invalid or expired reset link", exception.Message);
    }

    // 실패 - 만료된 토큰
    [Fact]
    public async Task ResetPasswordAsync_ExpiredToken_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var token = "expired-reset-token";
        var newPassword = "NewPassword123!";

        var user = new User
        {
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword("OldPassword123!"),
            Nickname = "testuser",
            Verified = true,
            EmailVerificationToken = null,
            PasswordResetToken = SecurityHelper.HashToken(token), // 토큰 해싱
            PasswordResetTokenExpiredAt = DateTime.UtcNow.AddHours(-1), // 만료됨
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.ResetPasswordAsync(token, newPassword)
        );

        Assert.Contains("Invalid or expired reset link", exception.Message);
    }

    // 실패 - 차단된 사용자
    [Fact]
    public async Task ResetPasswordAsync_BlockedUser_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var token = "valid-reset-token";
        var newPassword = "NewPassword123!";

        var user = new User
        {
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword("OldPassword123!"),
            Nickname = "testuser",
            Verified = true,
            EmailVerificationToken = null,
            PasswordResetToken = SecurityHelper.HashToken(token), // 토큰 해싱
            PasswordResetTokenExpiredAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // 사용자 차단
        var blockedUser = new BlockedUser
        {
            UserId = user.Id,
            Reason = "Test block",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            BlockedAt = DateTime.UtcNow
        };

        await _context.BlockedUsers.AddAsync(blockedUser);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.ResetPasswordAsync(token, newPassword)
        );

        Assert.Contains("Your account has been blocked", exception.Message);
    }

    // 비밀번호 재설정 성공 시 모든 RefreshToken 삭제
    [Fact]
    public async Task ResetPasswordAsync_Success_DeletesAllRefreshTokens()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var token = "valid-reset-token";
        var newPassword = "NewPassword123!";

        var user = new User
        {
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword("OldPassword123!"),
            Nickname = "testuser",
            Verified = true,
            EmailVerificationToken = null,
            PasswordResetToken = SecurityHelper.HashToken(token), // 토큰 해싱
            PasswordResetTokenExpiredAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // RefreshToken 3개 생성
        var refreshToken1 = new RefreshToken
        {
            UserId = user.Id,
            Token = "refresh-token-1",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        var refreshToken2 = new RefreshToken
        {
            UserId = user.Id,
            Token = "refresh-token-2",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        var refreshToken3 = new RefreshToken
        {
            UserId = user.Id,
            Token = "refresh-token-3",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        await _context.RefreshTokens.AddRangeAsync(refreshToken1, refreshToken2, refreshToken3);
        await _context.SaveChangesAsync();

        // RefreshToken 개수 확인
        var tokenCountBefore = await _context.RefreshTokens
            .Where(rt => rt.UserId == user.Id)
            .CountAsync();
        Assert.Equal(3, tokenCountBefore);

        // Act
        var response = await _authService.ResetPasswordAsync(token, newPassword);

        // Assert
        Assert.True(response.Success);

        // 모든 RefreshToken이 삭제되었는지 확인
        var tokenCountAfter = await _context.RefreshTokens
            .Where(rt => rt.UserId == user.Id)
            .CountAsync();
        Assert.Equal(0, tokenCountAfter);
    }

    #endregion
}
