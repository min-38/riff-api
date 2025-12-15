using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using api.Data;
using api.DTOs.Requests;
using api.Models;
using api.Services;
using DotNetEnv;

namespace api.Tests.Services;

public class AuthServiceTests : IDisposable
{
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
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

    #region 이메일 인증 요청

    // 정상
    [Fact]
    public async Task SendVerificationCodeAsync_NewEmail_Success()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync((User?)null);
        _emailServiceMock.Setup(x => x.SendVerificationEmailAsync(email, It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.SendVerificationCodeAsync(email);

        // Assert
        Assert.True(result);
        _emailServiceMock.Verify(x => x.SendVerificationEmailAsync(email, It.IsAny<string>()), Times.Once);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(user); // user는 null이 아니어야 하고
        Assert.False(user.Verified); // 인증되지 않은 상태여야 하며
        Assert.NotNull(user.VerificationCode); // 코드가 있어야 하고
        Assert.Equal(6, user.VerificationCode.Length); // 코드의 길이는 6자리 이어야 하고
        Assert.NotNull(user.VerificationToken); // 토큰이 있어야 한다.
    }

    // 차단된 이메일일 때
    [Fact]
    public async Task SendVerificationCodeAsync_BlockedEmail_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var blockedUser = new BlockedUser
        {
            Email = email,
            Reason = "Spam account",
            BlockedAt = DateTime.UtcNow,
            ExpiresAt = null
        };
        _context.BlockedUsers.Add(blockedUser);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.SendVerificationCodeAsync(email)
        );
        Assert.Equal("Spam account", exception.Message);
    }

    // 이미 인증 받은 이메일이 존재할 때
    [Fact]
    public async Task SendVerificationCodeAsync_AlreadyVerifiedEmail_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var verifiedUser = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Verified = true,
            Password = "hashedpassword",
            Nickname = "testuser",
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync(verifiedUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.SendVerificationCodeAsync(email)
        );
        Assert.Equal("Email already exists", exception.Message);
    }

    // 5번 시도한 이메일
    [Fact]
    public async Task SendVerificationCodeAsync_FiveAttempts_BlocksEmail()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Verified = false,
            Password = "",
            Nickname = "",
            Rating = 0.0,
            VerificationEmailAttempts = 5,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync(user);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.SendVerificationCodeAsync(email)
        );
        Assert.Contains("temporarily blocked for 24 hours", exception.Message);

        var blockedUser = await _context.BlockedUsers.FirstOrDefaultAsync(bu => bu.Email == email);
        Assert.NotNull(blockedUser);
        Assert.Equal("Too many verification attempts", blockedUser.Reason);
    }

    # endregion

    #region 이메일 인증 응답

    // 정상
    [Fact]
    public async Task VerifyEmailCodeAsync_ValidCode_ReturnsSessionToken()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var code = "123456";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Verified = false,
            Password = "",
            Nickname = "",
            Rating = 0.0,
            VerificationCode = code,
            VerificationToken = "token",
            VerificationTokenExpiry = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync(user);

        // Act
        var sessionToken = await _authService.VerifyEmailCodeAsync(email, code);

        // Assert
        Assert.NotNull(sessionToken); // 세션 토큰이 있어야 하고
        Assert.NotEmpty(sessionToken); // 세션 토큰이 빈 값이면 안되고
        Assert.True(user.Verified); // 인증된 유저로 바뀌어야 하고
        Assert.Null(user.VerificationCode); // 인증 코드는 null로 변경되고
        Assert.Null(user.VerificationToken); // 인증 토큰 또한 null로 변경되어야 하고
        Assert.NotNull(user.RegistrationSessionToken); // 가입 세션 토큰 값은 null이 아니어야 하며
        Assert.NotNull(user.RegistrationSessionExpiry); // 가입 세션 토큰 유효 시간도 null이 아니어야 하며
        Assert.Equal(sessionToken, user.RegistrationSessionToken); // 세션 토큰과 가입 세션 토큰 값이 같아야 한다.
    }

    // 인증코드 틀렸을 때
    [Fact]
    public async Task VerifyEmailCodeAsync_InvalidCode_ThrowsException()
    {
        // Arrange
        InitializeContext();

        var email = "test@test.com";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Verified = false,
            Password = "",
            Nickname = "",
            Rating = 0.0,
            VerificationCode = "123456",
            VerificationTokenExpiry = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync(user);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.VerifyEmailCodeAsync(email, "999999")
        );
        Assert.Equal("Invalid verification code", exception.Message);
    }

    // 만료된 코드일 때
    [Fact]
    public async Task VerifyEmailCodeAsync_ExpiredCode_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Verified = false,
            Password = "",
            Nickname = "",
            Rating = 0.0,
            VerificationCode = "123456",
            VerificationTokenExpiry = DateTime.UtcNow.AddHours(-1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync(user);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.VerifyEmailCodeAsync(email, "123456")
        );
        Assert.Equal("Verification code has expired", exception.Message);
    }

    #endregion

    #region 닉네임 사용 가능 여부 확인

    // 사용 가능
    [Fact]
    public async Task CheckNicknameAvailabilityAsync_Available_ReturnsTrue()
    {
        // Arrange
        InitializeContext();
        var nickname = "newuser";
        _userServiceMock.Setup(x => x.GetUserByNicknameAsync(nickname))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _authService.CheckNicknameAvailabilityAsync(nickname);

        // Assert
        Assert.True(result);
    }

    // 이미 존재하는 닉네임일 때
    [Fact]
    public async Task CheckNicknameAvailabilityAsync_AlreadyExists_ReturnsFalse()
    {
        // Arrange
        InitializeContext();
        var nickname = "test";
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            Nickname = nickname,
            Verified = true,
            Password = "hashedpassword",
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _userServiceMock.Setup(x => x.GetUserByNicknameAsync(nickname))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _authService.CheckNicknameAvailabilityAsync(nickname);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region 이메일 인증, 닉네임 확인까지 끝낸 후, 실제 회원가입

    // 정상
    [Fact]
    public async Task RegisterAsync_ValidRequest_Success()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var sessionToken = "valid_session_token";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Nickname = "",
            Verified = true,
            Password = "",
            Rating = 0.0,
            RegistrationSessionToken = sessionToken,
            RegistrationSessionExpiry = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync(user);
        _userServiceMock.Setup(x => x.GetUserByNicknameAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);
        _userServiceMock.Setup(x => x.GetUserByPhoneAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);

        var request = new RegisterRequest
        {
            Email = email,
            SessionToken = sessionToken,
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "test",
            Phone = "010-1234-5678"
        };

        // Act
        var response = await _authService.RegisterAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(user.Id, response.UserId);
        Assert.Equal(email, response.Email);
        Assert.Equal("test", response.Nickname);
        Assert.True(response.Verified);
        Assert.Null(response.Token);
        Assert.Null(response.RefreshToken);

        Assert.Null(user.RegistrationSessionToken);
        Assert.Null(user.RegistrationSessionExpiry);
        Assert.NotEmpty(user.Password);
    }

    // 세션 토큰일 틀릴 때
    [Fact]
    public async Task RegisterAsync_InvalidSessionToken_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Verified = true,
            Password = "",
            Nickname = "",
            Rating = 0.0,
            RegistrationSessionToken = "correct_token",
            RegistrationSessionExpiry = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync(user);

        var request = new RegisterRequest
        {
            Email = email,
            SessionToken = "wrong_token",
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "test"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.RegisterAsync(request)
        );
        Assert.Equal("Invalid session token.", exception.Message);
    }

    // 세션 토큰이 만료되었을 때
    [Fact]
    public async Task RegisterAsync_ExpiredSession_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";
        var sessionToken = "expired_token";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Verified = true,
            Password = "",
            Nickname = "",
            Rating = 0.0,
            RegistrationSessionToken = sessionToken,
            RegistrationSessionExpiry = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync(user);

        var request = new RegisterRequest
        {
            Email = email,
            SessionToken = sessionToken,
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "test"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.RegisterAsync(request)
        );
        Assert.Equal("Session expired. Please verify your email again.", exception.Message);
    }

    // 세션 토큰이 다른 이메일로부터 회원가입을 시도될 때
    [Fact]
    public async Task RegisterAsync_DifferentEmailWithValidToken_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var emailB = "b@test.com"; // 소문자로 저장
        var AToken = "A_session_token";

        var bob = new User
        {
            Id = Guid.NewGuid(),
            Email = emailB,
            Verified = true,
            Password = "",
            Nickname = "",
            Rating = 0.0,
            RegistrationSessionToken = "B_session_token",
            RegistrationSessionExpiry = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(emailB))
            .ReturnsAsync(bob);

        var request = new RegisterRequest
        {
            Email = emailB,
            SessionToken = AToken,
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "test"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.RegisterAsync(request)
        );
        Assert.Equal("Invalid session token.", exception.Message);
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

    // 유효하지 않은 이메일
    [Fact]
    public async Task LogInAsync_InvalidEmail_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var email = "test@test.com";

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(email))
            .ReturnsAsync((User?)null);

        var request = new LoginRequest
        {
            Email = email,
            Password = "Password123!"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.LogInAsync(request)
        );
        Assert.Equal("Email or Password is not correct", exception.Message);
    }

    // 패스워드가 틀렸을 때
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
        Assert.Equal("Email or Password is not correct", exception.Message);
    }

    // 인증되지 않은 유저일 때
    [Fact]
    public async Task LogInAsync_UnverifiedUser_ThrowsException()
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
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

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
        Assert.Equal("Account is not verified", exception.Message);
    }

    // 차단당한 유저일 때
    [Fact]
    public async Task LogInAsync_BlockedUser_ThrowsException()
    {
        // Arrange
        InitializeContext();
        var email = "blocked@example.com";
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
            Reason = "Account suspended",
            BlockedAt = DateTime.UtcNow,
            ExpiresAt = null
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
        Assert.Equal("Account suspended", exception.Message);
    }

    #endregion

    #region 로그아웃 테스트

    // 정상적인 로그아웃
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

    // refresh token이 없을 때 로그아웃
    [Fact]
    public async Task LogOutAsync_NoRefreshToken_Success()
    {
        // Arrange
        InitializeContext();

        // Act
        var result = await _authService.LogOutAsync(null);

        // Assert
        Assert.True(result);
    }

    // 존재하지 않은 token이 들어왔을 때
    [Fact]
    public async Task LogOutAsync_NonExistentToken_Success()
    {
        // Arrange
        InitializeContext();

        // Act
        var result = await _authService.LogOutAsync("non_existent_token");

        // Assert
        Assert.True(result); // 존재하지 않는 토큰이어도 성공 반환
    }

    // 이미 취소된 token일 때
    [Fact]
    public async Task LogOutAsync_AlreadyRevokedToken_Success()
    {
        // Arrange
        InitializeContext();
        var userId = Guid.NewGuid();
        var refreshToken = "already_revoked_token";

        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = refreshToken,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = DateTime.UtcNow.AddMinutes(-10) // 이미 무효화됨
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        // Act
        var result = await _authService.LogOutAsync(refreshToken);

        // Assert
        Assert.True(result); // 이미 무효화된 토큰이어도 성공 반환
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

    // 올바르지 않은 token일 때
    [Fact]
    public async Task RefreshAccessTokenAsync_InvalidToken_ThrowsException()
    {
        // Arrange
        InitializeContext();

        _tokenServiceMock
            .Setup(x => x.RefreshAccessTokenAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Invalid refresh token"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.RefreshAccessTokenAsync("invalid_token")
        );
        Assert.Equal("Invalid refresh token", exception.Message);
    }

    // 만료된 token일 때
    [Fact]
    public async Task RefreshAccessTokenAsync_ExpiredToken_ThrowsException()
    {
        // Arrange
        InitializeContext();

        _tokenServiceMock
            .Setup(x => x.RefreshAccessTokenAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Refresh token is expired or revoked"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.RefreshAccessTokenAsync("expired_token")
        );
        Assert.Equal("Refresh token is expired or revoked", exception.Message);
    }

    #endregion
}
