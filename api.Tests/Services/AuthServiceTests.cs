using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using api.Data;
using api.DTOs.Requests;
using api.Models;
using api.Services;
using DotNetEnv;
using System.Diagnostics.CodeAnalysis;
using FirebaseAdmin.Auth;

namespace api.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<IFirebaseService> _firebaseServiceMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private ApplicationDbContext _context = null!;
    private AuthService _authService = null!;

    public AuthServiceTests()
    {
        Env.Load();

        _loggerMock = new Mock<ILogger<AuthService>>();
        _configurationMock = new Mock<IConfiguration>();
        _userServiceMock = new Mock<IUserService>();
        _firebaseServiceMock = new Mock<IFirebaseService>();
        _tokenServiceMock = new Mock<ITokenService>();
    }

    private void InitializeContext()
    {
        // 메모리에 가상 데이터베이스 생성
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // 데이터베이스 이름은 랜덤으로 생성
            .Options;
        _context = new ApplicationDbContext(options);

        _authService = new AuthService(
            _context,
            _loggerMock.Object,
            _configurationMock.Object,
            _userServiceMock.Object,
            _firebaseServiceMock.Object,
            _tokenServiceMock.Object
        );
    }

    // 회원가입 - 정상
    [Fact]
    public async Task RegisterAsync_ShouldAuthResponse_WhenValidRequest()
    {
        // Given
        InitializeContext(); // 테스트 전에 데이터베이스 초기화

        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "testuser",
            Phone = "010-1234-5678",
            FirebaseIdToken = "qwer"
        };

        _firebaseServiceMock
            .Setup(x => x.VerifyIdTokenAsync(It.IsAny<string>())) // firebase 토큰 인증 시
            .ReturnsAsync((FirebaseToken)null!); // FirebaseToken은 생성 불가하므로 null 반환

        _userServiceMock
            .Setup(x => x.GetUserByEmailAsync(It.IsAny<string>())) // 이메일 중복 체크 시
            .ReturnsAsync((User?)null); // 존재하지 않음

        _userServiceMock
            .Setup(x => x.GetUserByNicknameAsync(It.IsAny<string>())) // 닉네임 중복 체크 시
            .ReturnsAsync((User?)null); // 존재하지 않음

        // When
        var response = await _authService.RegisterAsync(request);

        // Then
        Assert.NotNull(response); // Null이면 안되고
        Assert.NotEqual(Guid.Empty, response.UserId);
        Assert.Equal(request.Email, response.Email); // 이메일이 같아야 하고
        Assert.Equal(request.Nickname, response.Nickname); // 닉네임이 같아야 하고
        Assert.True(response.Verified); // Firebase 인증을 통한 회원가입이므로 인증된 상태여야 함
    }

    // 회원가입 - Firebase 인증 실패일 때
    [Fact]
    public async Task RegisterAsync_ShouldThrowException_WhenFirebaseVertifyFailed()
    {
        // Given
        InitializeContext();

        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Password123!",
            Nickname = "testuser",
            Phone = "010-1234-5678",
            FirebaseIdToken = "qwer"
        };

        // When
        _firebaseServiceMock
            .Setup(x => x.VerifyIdTokenAsync(It.IsAny<string>())) // firebase 토큰 인증 시
            .ThrowsAsync(new Exception("Firebase authentication failed")); // Firebase 인증 실패 예외 발생

        // Then
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.RegisterAsync(request)
        );
        Assert.Equal("Invalid Firebase ID token", exception.Message); // 예외 메시지가 "Invalid Firebase ID token"이어야 함
    }

    [Fact]
    public async Task RegisterAsync_ShouldThrowException_WhenEmailAlreadyExists() // 회원가입 - 이미 유저 테이블에 존재하는 이메일일 때
    {
        // Given
        InitializeContext();

        // 이미 존재할 사용자 생성
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            Password = "hashedpassword",
            Nickname = "existing",
            Phone = "010-1234-5678",
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(existingUser);
        await _context.SaveChangesAsync();

        // When
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Password123!",
            Nickname = "testuser",
            Phone = "010-1234-5678",
            FirebaseIdToken = "qwer"
        };

        _firebaseServiceMock
            .Setup(x => x.VerifyIdTokenAsync(It.IsAny<string>())) // firebase 토큰 인증 시
            .ReturnsAsync((FirebaseToken)null!); // FirebaseToken은 생성 불가하므로 null 반환

        _userServiceMock
            .Setup(x => x.GetUserByEmailAsync(It.IsAny<string>())) // 이메일 중복 체크 시
            .ReturnsAsync(existingUser); // 존재하는 사용자 반환

        // Then
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.RegisterAsync(request)
        );
        Assert.Equal("Email already exists", exception.Message); // 예외 메시지가 "Email already exists"이어야 함
    }

    // 회원가입 - 이미 유저 테이블에 존재하는 닉네임일 때
    [Fact]
    public async Task RegisterAsync_ShouldThrowException_WhenNicknameAlreadyExists()
    {
        // Given
        InitializeContext();

        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "existing@example.com",
            Password = "hashedpassword",
            Nickname = "testuser",  // 중복될 닉네임
            Phone = "010-1234-5678",
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(existingUser);
        await _context.SaveChangesAsync();

        // 회원가입 요청 생성 (같은 닉네임)
        var request = new RegisterRequest
        {
            Email = "newuser@example.com",
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "testuser",  // 중복
            Phone = "010-9876-5432"
        };

        // When
        _firebaseServiceMock
            .Setup(x => x.VerifyIdTokenAsync(It.IsAny<string>())) // firebase 토큰 인증 시
            .ReturnsAsync((FirebaseToken)null!); // FirebaseToken은 생성 불가하므로 null 반환

        _userServiceMock
            .Setup(x => x.GetUserByEmailAsync(It.IsAny<string>())) // 이메일 중복 체크 시
            .ReturnsAsync((User?)null); // 존재하지 않는 사용자 반환

        _userServiceMock
            .Setup(x => x.GetUserByNicknameAsync(It.IsAny<string>())) // 닉네임 중복 체크 시
            .ReturnsAsync(existingUser); // 존재하는 사용자 반환

        // Then
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.RegisterAsync(request)
        );
        Assert.Equal("Nickname already exists", exception.Message);
    }

    // 로그인 - 정상
    [Fact]
    public async Task LoginAsync_ShouldAuthResponse_WhenValidRequest()
    {
        // Given
        InitializeContext(); // 테스트 전에 데이터베이스 초기화

        var request = new LoginRequest
        {
            Email = "test@test.com",
            Password = "Password123!"
        };

        // 실제 BCrypt로 해시 생성
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword("Password123!");

        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            Password = hashedPassword,
            Nickname = "existing",
            Phone = "010-1234-5678",
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _userServiceMock
            .Setup(x => x.GetUserByEmailAsync(It.IsAny<string>())) // 이메일 중복 체크 시
            .ReturnsAsync(existingUser); // 존재함

        _tokenServiceMock
          .Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
          .Returns("test_access_token");

        _tokenServiceMock
            .Setup(x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>()))
            .ReturnsAsync("test_refresh_token");


        // When
        var response = await _authService.LogInAsync(request);

        // Then
        Assert.NotNull(response); // Null이면 안되고
        Assert.NotEqual(Guid.Empty, response.UserId);
        Assert.Equal(request.Email, response.Email); // 이메일이 같아야 하고
        Assert.Equal(existingUser.Nickname, response.Nickname); // 닉네임이 같아야 하고
        Assert.NotNull(response.Token); // jwt 토큰 값이 있어야 하고
        Assert.NotNull(response.RefreshToken); // jwt refresh 토큰 값이 있어야 하고
        Assert.NotNull(response.ExpiresAt); // jwt 토큰 만료 시간이 있어야 하고
        Assert.True(response.Verified); // Firebase 인증을 통한 회원가입이므로 인증된 상태여야 함
    }

    // 로그인 - 이메일이 없음
    [Fact]
    public async Task LoginAsync_ShouldThrowException_WhenEmailNotFound()
    {
        // Given
        InitializeContext(); // 테스트 전에 데이터베이스 초기화

        var request = new LoginRequest
        {
            Email = "test@test.com",
            Password = "Password123!"
        };

        _userServiceMock
            .Setup(x => x.GetUserByEmailAsync(It.IsAny<string>())) // 이메일 체크
            .ReturnsAsync((User?)null); // 존재하지 않는 계정


        // When
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.LogInAsync(request)
        );

        // Then
        Assert.Equal("Email or Password is not correct", exception.Message);
    }

    // 로그인 - 패스워드 불일치
    [Fact]
    public async Task LoginAsync_ShouldThrowException_WhenPasswordIsIncorrect()
    {
        // Given
        InitializeContext();

        var request = new LoginRequest
        {
            Email = "test@test.com",
            Password = "WrongPassword123!" // 틀린 비밀번호
        };

        // 올바른 비밀번호로 해시 생성
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword("CorrectPassword123!");

        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            Password = hashedPassword, // 실제 BCrypt 해시
            Nickname = "existing",
            Phone = "010-1234-5678",
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _userServiceMock
            .Setup(x => x.GetUserByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(existingUser);

        // When & Then
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.LogInAsync(request)
        );

        Assert.Equal("Email or Password is not correct", exception.Message);
    }

    // 로그인 - 인증되지 않은 계정
    [Fact]
    public async Task LoginAsync_ShouldThrowException_WhenUserIsNotVerified()
    {
        // Given
        InitializeContext();

        var request = new LoginRequest
        {
            Email = "test@test.com",
            Password = "Password123!"
        };

        // 올바른 비밀번호로 해시 생성
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword("Password123!");

        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            Password = hashedPassword, // 실제 BCrypt 해시
            Nickname = "existing",
            Phone = "010-1234-5678",
            Verified = false,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _userServiceMock
            .Setup(x => x.GetUserByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(existingUser);

        // When & Then
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.LogInAsync(request)
        );

        Assert.Equal("Account is not verified", exception.Message);
    }

    // 로그아웃 - 정상 (토큰 있음)
    [Fact]
    public async Task LogOutAsync_ShouldReturnTrue_WhenValidRefreshToken()
    {
        // Given
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

        // When
        var result = await _authService.LogOutAsync(refreshToken);

        // Then
        Assert.True(result);

        // Refresh Token이 무효화되었는지 확인
        var revokedToken = await _context.RefreshTokens.FindAsync(refreshTokenEntity.Id);
        Assert.NotNull(revokedToken);
        Assert.NotNull(revokedToken.RevokedAt);
    }

    // 로그아웃 - 정상 (토큰 없음)
    [Fact]
    public async Task LogOutAsync_ShouldReturnTrue_WhenNoRefreshToken()
    {
        // Given
        InitializeContext();

        // When
        var result = await _authService.LogOutAsync(null);

        // Then
        Assert.True(result);
    }

    // 로그아웃 - 존재하지 않는 토큰
    [Fact]
    public async Task LogOutAsync_ShouldReturnTrue_WhenTokenNotFound()
    {
        // Given
        InitializeContext();

        // When
        var result = await _authService.LogOutAsync("non_existent_token");

        // Then
        Assert.True(result); // 존재하지 않는 토큰이어도 성공 반환
    }

    // 로그아웃 - 이미 무효화된 토큰
    [Fact]
    public async Task LogOutAsync_ShouldReturnTrue_WhenTokenAlreadyRevoked()
    {
        // Given
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

        // When
        var result = await _authService.LogOutAsync(refreshToken);

        // Then
        Assert.True(result); // 이미 무효화된 토큰이어도 성공 반환
    }

    // 토큰 갱신 - 정상
    [Fact]
    public async Task RefreshAccessTokenAsync_ShouldReturnAuthResponse_WhenValidRefreshToken()
    {
        // Given
        InitializeContext();

        var userId = Guid.NewGuid();
        var refreshToken = "valid_refresh_token";

        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            Password = "hashedpassword",
            Nickname = "testuser",
            Phone = "010-1234-5678",
            Verified = true,
            Rating = 0.0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = refreshToken,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = null,
            User = user
        };

        _context.Users.Add(user);
        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        _tokenServiceMock
            .Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("new_access_token");

        _tokenServiceMock
            .Setup(x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>()))
            .ReturnsAsync("new_refresh_token");

        _tokenServiceMock
            .Setup(x => x.RefreshAccessTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(("new_access_token", "new_refresh_token", user));

        // When
        var response = await _authService.RefreshAccessTokenAsync(refreshToken);

        // Then
        Assert.NotNull(response);
        Assert.Equal(userId, response.UserId);
        Assert.Equal(user.Email, response.Email);
        Assert.Equal(user.Nickname, response.Nickname);
        Assert.NotNull(response.Token);
        Assert.NotNull(response.RefreshToken);
        Assert.True(response.Verified);
    }

    // 토큰 갱신 - 유효하지 않은 토큰
    [Fact]
    public async Task RefreshAccessTokenAsync_ShouldThrowException_WhenInvalidRefreshToken()
    {
        // Given
        InitializeContext();

        _tokenServiceMock
            .Setup(x => x.RefreshAccessTokenAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Invalid refresh token"));

        // When & Then
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.RefreshAccessTokenAsync("invalid_token")
        );

        Assert.Equal("Invalid refresh token", exception.Message);
    }

    // 토큰 갱신 - 만료된 토큰
    [Fact]
    public async Task RefreshAccessTokenAsync_ShouldThrowException_WhenExpiredRefreshToken()
    {
        // Given
        InitializeContext();

        _tokenServiceMock
            .Setup(x => x.RefreshAccessTokenAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Refresh token is expired or revoked"));

        // When & Then
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _authService.RefreshAccessTokenAsync("expired_token")
        );

        Assert.Equal("Refresh token is expired or revoked", exception.Message);
    }
}
