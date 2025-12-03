using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using api.Controllers;
using api.DTOs.Requests;
using api.DTOs.Responses;
using api.Services;

namespace api.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _authServiceMock = new Mock<IAuthService>();
        _loggerMock = new Mock<ILogger<AuthController>>();
        _controller = new AuthController(_authServiceMock.Object, _loggerMock.Object);
    }

    // 회원가입 - 정상 처리
    [Fact]
    public async Task Register_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "testuser",
            Phone = "010-1234-5678",
            FirebaseIdToken = "test_token"
        };

        var expectedResponse = new AuthResponse
        {
            UserId = Guid.NewGuid(),
            Email = request.Email,
            Nickname = request.Nickname,
            Verified = false
        };

        _authServiceMock
            .Setup(x => x.RegisterAsync(It.IsAny<RegisterRequest>())) // RegisterAsync 함수가 실행되면
            .ReturnsAsync(expectedResponse); // expectedResponse를 리턴

        // Act
        var result = await _controller.Register(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result); // OkObjectResult를 리턴
        var response = Assert.IsType<AuthResponse>(okResult.Value); // okResult.Value를 AuthResponse로 변환
        Assert.Equal(expectedResponse.Email, response.Email); // expectedResponse.Email과 response.Email이 일치
        Assert.Equal(expectedResponse.Nickname, response.Nickname); // expectedResponse.Nickname과 response.Nickname이 일치
        Assert.False(response.Verified); // response.Verified가 false
    }

    // 회원가입 - 이메일 중복
    [Fact]
    public async Task Register_ShouldReturnBadRequest_WhenEmailAlreadyExists()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "testuser",
            Phone = "010-1234-5678",
            FirebaseIdToken = "test_token"
        };

        _authServiceMock
            .Setup(x => x.RegisterAsync(It.IsAny<RegisterRequest>()))
            .ThrowsAsync(new InvalidOperationException("Email already exists"));

        // Act
        var result = await _controller.Register(request);

        // Assert
        var conflictObjectResult = Assert.IsType<ConflictObjectResult>(result.Result);
        var responseValue = conflictObjectResult.Value;
        Assert.NotNull(responseValue);

        var messageProperty = responseValue.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(responseValue) as string;
        Assert.Equal("Email already exists", message);
    }

    // 회원가입 - 닉네임 중복
    [Fact]
    public async Task Register_ShouldReturnBadRequest_WhenNicknameAlreadyExists()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "testuser",
            Phone = "010-1234-5678",
            FirebaseIdToken = "test_token"
        };

        _authServiceMock
            .Setup(x => x.RegisterAsync(It.IsAny<RegisterRequest>()))
            .ThrowsAsync(new InvalidOperationException("Nickname already exists"));

        // Act
        var result = await _controller.Register(request);

        // Assert
        var conflictObjectResult = Assert.IsType<ConflictObjectResult>(result.Result);
        var responseValue = conflictObjectResult.Value;
        Assert.NotNull(responseValue);

        var messageProperty = responseValue.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(responseValue) as string;
        Assert.Equal("Nickname already exists", message);
    }

    // 회원가입 - Firebase 인증 실패
    [Fact]
    public async Task Register_ShouldReturnBadRequest_WhenFirebaseAuthFailed()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "testuser",
            Phone = "010-1234-5678",
            FirebaseIdToken = "test_token"
        };

        _authServiceMock
            .Setup(x => x.RegisterAsync(It.IsAny<RegisterRequest>()))
            .ThrowsAsync(new InvalidOperationException("Firebase authentication failed"));

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var responseValue = badRequestResult.Value;
        Assert.NotNull(responseValue);

        var messageProperty = responseValue.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(responseValue) as string;
        Assert.Equal("Firebase authentication failed", message);
    }

    // 회원가입 - 예외 발생
    [Fact]
    public async Task Register_ShouldReturnInternalServerError_WhenUnexpectedExceptionOccurs()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "testuser",
            Phone = "010-1234-5678"
        };

        _authServiceMock
            .Setup(x => x.RegisterAsync(It.IsAny<RegisterRequest>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var result = await _controller.Register(request);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusCodeResult.StatusCode);

        var responseValue = statusCodeResult.Value;
        Assert.NotNull(responseValue);

        var messageProperty = responseValue.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(responseValue) as string;
        Assert.Equal("An error occurred during registration", message);
    }

    // 로그인 - 정상
    [Fact]
    public async Task Login_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "Password123!",
        };

        _authServiceMock
            .Setup(x => x.LogInAsync(It.IsAny<LoginRequest>()))
            .ReturnsAsync(new AuthResponse
            {
                Token = "test_token",
                RefreshToken = "test_refresh_token"
            });

        // Act
        var result = await _controller.Login(request);

        // Assert
        var statusCodeResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, statusCodeResult.StatusCode);

        var responseValue = statusCodeResult.Value;
        Assert.NotNull(responseValue);

        var tokenProperty = responseValue.GetType().GetProperty("Token");
        Assert.NotNull(tokenProperty); // 토큰이 없으면 안되고
        var refreshTokenProperty = responseValue.GetType().GetProperty("RefreshToken");
        Assert.NotNull(refreshTokenProperty); // 리프레시 토큰이 없으면 안되고
        var tokenExpiresAtProperty = responseValue.GetType().GetProperty("ExpiresAt");
        Assert.NotNull(tokenExpiresAtProperty); // 토큰 만료일이 없으면 안되고
    }

    // 로그인 - 이메일 없음
    [Fact]
    public async Task Login_ShouldReturnBadRequest_WhenEmailNotFound()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "Password123!",
        };

        _authServiceMock
            .Setup(x => x.LogInAsync(It.IsAny<LoginRequest>()))
            .ThrowsAsync(new InvalidOperationException("Email is not exist"));

        // Act
        var result = await _controller.Login(request);

        // Assert
        var statusCodeResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(400, statusCodeResult.StatusCode);

        var responseValue = statusCodeResult.Value;
        Assert.NotNull(responseValue);

        var messageProperty = responseValue.GetType().GetProperty("message");
        Assert.NotNull(messageProperty); // 메시지가 없으면 안되고
        var message = messageProperty.GetValue(responseValue) as string;
        Assert.Equal("Email is not exist", message);
    }

    // 로그인 - 인증 안된 계정
    [Fact]
    public async Task Login_ShouldReturnBadRequest_WhenNotVerified()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "Password123!",
        };

        _authServiceMock
            .Setup(x => x.LogInAsync(It.IsAny<LoginRequest>()))
            .ThrowsAsync(new InvalidOperationException("Account is not verified"));

        // Act
        var result = await _controller.Login(request);

        // Assert
        var statusCodeResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(400, statusCodeResult.StatusCode);

        var responseValue = statusCodeResult.Value;
        Assert.NotNull(responseValue);

        var messageProperty = responseValue.GetType().GetProperty("message");
        Assert.NotNull(messageProperty); // 메시지가 없으면 안되고
        var message = messageProperty.GetValue(responseValue) as string;
        Assert.Equal("Account is not verified", message);
    }

    // 로그인 - 비밀번호 안맞음
    [Fact]
    public async Task Login_ShouldReturnBadRequest_WhenPasswordNotMatch()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "Password123!",
        };

        _authServiceMock
            .Setup(x => x.LogInAsync(It.IsAny<LoginRequest>()))
            .ThrowsAsync(new InvalidOperationException("Invalid password"));

        // Act
        var result = await _controller.Login(request);

        // Assert
        var statusCodeResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(400, statusCodeResult.StatusCode);

        var responseValue = statusCodeResult.Value;
        Assert.NotNull(responseValue);

        var messageProperty = responseValue.GetType().GetProperty("message");
        Assert.NotNull(messageProperty); // 메시지가 없으면 안되고
        var message = messageProperty.GetValue(responseValue) as string;
        Assert.Equal("Invalid password", message);
    }

    // 로그인 - 예외 발생
    [Fact]
    public async Task Login_ShouldReturnInternalServerError_WhenUnexpectedExceptionOccurs()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "Password123!",
        };

        _authServiceMock
            .Setup(x => x.LogInAsync(It.IsAny<LoginRequest>()))
            .ThrowsAsync(new Exception("An error occurred during login"));

        // Act
        var result = await _controller.Login(request);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusCodeResult.StatusCode);

        var responseValue = statusCodeResult.Value;
        Assert.NotNull(responseValue);

        var messageProperty = responseValue.GetType().GetProperty("message");
        Assert.NotNull(messageProperty); // 메시지가 없으면 안되고
        var message = messageProperty.GetValue(responseValue) as string;
        Assert.Equal("An error occurred during login", message);
    }

    // 로그아웃 - 정상 (토큰 있음)
    [Fact]
    public async Task Logout_ShouldReturnOk_WhenValidRefreshToken()
    {
        // Arrange
        var request = new RefreshTokenRequest
        {
            RefreshToken = "valid_refresh_token"
        };

        _authServiceMock
            .Setup(x => x.LogOutAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Logout(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var responseValue = okResult.Value;
        Assert.NotNull(responseValue);

        var messageProperty = responseValue.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(responseValue) as string;
        Assert.Equal("Logout successful", message);
    }

    // 로그아웃 - 정상 (토큰 없음)
    [Fact]
    public async Task Logout_ShouldReturnOk_WhenNoRefreshToken()
    {
        // Arrange
        _authServiceMock
            .Setup(x => x.LogOutAsync(null))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Logout(null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    // 로그아웃 - 예외 발생
    [Fact]
    public async Task Logout_ShouldReturnInternalServerError_WhenExceptionOccurs()
    {
        // Arrange
        var request = new RefreshTokenRequest
        {
            RefreshToken = "valid_refresh_token"
        };

        _authServiceMock
            .Setup(x => x.LogOutAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.Logout(request);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);

        var responseValue = statusCodeResult.Value;
        Assert.NotNull(responseValue);

        var messageProperty = responseValue.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(responseValue) as string;
        Assert.Equal("An error occurred during logout", message);
    }

    // 토큰 갱신 - 정상
    [Fact]
    public async Task RefreshToken_ShouldReturnOk_WhenValidRefreshToken()
    {
        // Arrange
        var request = new RefreshTokenRequest
        {
            RefreshToken = "valid_refresh_token"
        };

        var expectedResponse = new AuthResponse
        {
            UserId = Guid.NewGuid(),
            Email = "test@example.com",
            Nickname = "testuser",
            Verified = true,
            Token = "new_access_token",
            RefreshToken = "new_refresh_token",
            ExpiresAt = DateTime.UtcNow.AddMinutes(60)
        };

        _authServiceMock
            .Setup(x => x.RefreshAccessTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AuthResponse>(okResult.Value);
        Assert.NotNull(response.Token);
        Assert.NotNull(response.RefreshToken);
        Assert.Equal(expectedResponse.Email, response.Email);
    }

    // 토큰 갱신 - 유효하지 않은 토큰
    [Fact]
    public async Task RefreshToken_ShouldReturnBadRequest_WhenInvalidRefreshToken()
    {
        // Arrange
        var request = new RefreshTokenRequest
        {
            RefreshToken = "invalid_refresh_token"
        };

        _authServiceMock
            .Setup(x => x.RefreshAccessTokenAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Invalid refresh token"));

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var responseValue = badRequestResult.Value;
        Assert.NotNull(responseValue);

        var messageProperty = responseValue.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(responseValue) as string;
        Assert.Equal("Invalid refresh token", message);
    }

    // 토큰 갱신 - 만료된 토큰
    [Fact]
    public async Task RefreshToken_ShouldReturnBadRequest_WhenExpiredRefreshToken()
    {
        // Arrange
        var request = new RefreshTokenRequest
        {
            RefreshToken = "expired_refresh_token"
        };

        _authServiceMock
            .Setup(x => x.RefreshAccessTokenAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Refresh token is expired or revoked"));

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var responseValue = badRequestResult.Value;
        Assert.NotNull(responseValue);

        var messageProperty = responseValue.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(responseValue) as string;
        Assert.Equal("Refresh token is expired or revoked", message);
    }

    // 토큰 갱신 - 예외 발생
    [Fact]
    public async Task RefreshToken_ShouldReturnInternalServerError_WhenExceptionOccurs()
    {
        // Arrange
        var request = new RefreshTokenRequest
        {
            RefreshToken = "valid_refresh_token"
        };

        _authServiceMock
            .Setup(x => x.RefreshAccessTokenAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.RefreshToken(request);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusCodeResult.StatusCode);

        var responseValue = statusCodeResult.Value;
        Assert.NotNull(responseValue);

        var messageProperty = responseValue.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(responseValue) as string;
        Assert.Equal("An error occurred during token refresh", message);
    }
}
