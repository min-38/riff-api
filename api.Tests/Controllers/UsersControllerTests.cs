using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using api.Controllers;
using api.Data;
using api.Services;

namespace api.Tests.Controllers;

public class UsersControllerTests
{
    private readonly Mock<ApplicationDbContext> _contextMock;
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<ILogger<UsersController>> _loggerMock;
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        _contextMock = new Mock<ApplicationDbContext>();
        _userServiceMock = new Mock<IUserService>();
        _loggerMock = new Mock<ILogger<UsersController>>();
        _controller = new UsersController(_contextMock.Object, _userServiceMock.Object, _loggerMock.Object);
    }

    #region CheckEmail Tests

    // 이메일 중복 확인 - 중복되지 않은 이메일
    [Fact]
    public async Task CheckEmail_ShouldReturnOk_WhenEmailIsAvailable()
    {
        // Given
        var email = "test@test.com";
        _userServiceMock
            .Setup(x => x.IsEmailAvailableAsync(email)) // IsEmailAvailableAsync 힘수가 실행되면
            .ReturnsAsync(true); // true 리턴

        // When
        var result = await _controller.CheckEmail(email); // CheckEmail 실행

        // Then
        var okResult = Assert.IsType<OkObjectResult>(result); // OkObjectResult 리턴
        var responseValue = okResult.Value;
        Assert.NotNull(responseValue); // 응답이 null이 아니어야 함

        var availableProperty = responseValue.GetType().GetProperty("available"); // available 프로퍼티 가져오기
        Assert.NotNull(availableProperty); // available 프로퍼티가 null이 아니어야 함
        var available = (bool?)availableProperty.GetValue(responseValue); // available 프로퍼티 값 가져오기
        Assert.True(available); // available이 true이어야 함

        var messageProperty = responseValue.GetType().GetProperty("message"); // message 프로퍼티 가져오기
        Assert.NotNull(messageProperty); // message 프로퍼티가 null이 아니어야 함
        var message = messageProperty.GetValue(responseValue) as string; // message 프로퍼티 값 가져오기
        Assert.Equal("Email is available", message); // message가 "Email is available"이어야 함
    }

    // 이메일 중복 확인 - 중복된 이메일
    [Fact]
    public async Task CheckEmail_ShouldReturnOk_WhenEmailIsNotAvailable()
    {
        // Given
        var email = "test@test.com";
        _userServiceMock
            .Setup(x => x.IsEmailAvailableAsync(email)) // IsEmailAvailableAsync 힘수가 실행되면
            .ReturnsAsync(false); // false 리턴

        // When
        var result = await _controller.CheckEmail(email); // CheckEmail 실행

        // Then
        var okResult = Assert.IsType<OkObjectResult>(result); // OkObjectResult 리턴
        var responseValue = okResult.Value;
        Assert.NotNull(responseValue); // 응답이 null이 아니어야 함

        var availableProperty = responseValue.GetType().GetProperty("available"); // available 프로퍼티 가져오기
        Assert.NotNull(availableProperty); // available 프로퍼티가 null이 아니어야 함
        var available = (bool?)availableProperty.GetValue(responseValue); // available 프로퍼티 값 가져오기
        Assert.False(available); // available이 false이어야 함

        var messageProperty = responseValue.GetType().GetProperty("message"); // message 프로퍼티 가져오기
        Assert.NotNull(messageProperty); // message 프로퍼티가 null이 아니어야 함
        var message = messageProperty.GetValue(responseValue) as string; // message 프로퍼티 값 가져오기
        Assert.Equal("Email already exists", message); // message가 "Email already exists"이어야 함
    }

    // 이메일 중복 확인 - 빈 문자열이 들어왔을 때
    [Fact]
    public async Task CheckEmail_ShouldReturnBadRequest_WhenEmailIsEmpty()
    {
        // Given
        var result = await _controller.CheckEmail(""); // CheckEmail 실행

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result); // BadRequestObjectResult 리턴
        var responseValue = badRequestResult.Value;
        Assert.NotNull(responseValue); // 응답이 null이 아니어야 함

        var messageProperty = responseValue.GetType().GetProperty("message"); // message 프로퍼티 가져오기
        Assert.NotNull(messageProperty); // message 프로퍼티가 null이 아니어야 함
        var message = messageProperty.GetValue(responseValue) as string; // message 프로퍼티 값 가져오기
        Assert.Equal("Email is required", message); // message가 "Email is required"이어야 함
    }

    // 이메일 중복 확인 - null이 들어왔을 때
    [Fact]
    public async Task CheckEmail_ShouldReturnBadRequest_WhenEmailIsNull()
    {
        // Act
        var result = await _controller.CheckEmail(null!); // CheckEmail 실행

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result); // BadRequestObjectResult 리턴
        var responseValue = badRequestResult.Value;
        Assert.NotNull(responseValue); // 응답이 null이 아니어야 함

        var messageProperty = responseValue.GetType().GetProperty("message"); // message 프로퍼티 가져오기
        Assert.NotNull(messageProperty); // message 프로퍼티가 null이 아니어야 함
        var message = messageProperty.GetValue(responseValue) as string; // message 프로퍼티 값 가져오기
        Assert.Equal("Email is required", message); // message가 "Email is required"이어야 함
    }

    #endregion

    #region CheckNickname Tests

    // 닉네임 중복 확인 - 존재하지 않는 닉네임
    [Fact]
    public async Task CheckNickname_ShouldReturnOk_WhenNicknameIsAvailable()
    {
        // Given
        var nickname = "availablenick";
        _userServiceMock
            .Setup(x => x.IsNicknameAvailableAsync(nickname))
            .ReturnsAsync(true);

        // When
        var result = await _controller.CheckNickname(nickname);

        // Then
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseValue = okResult.Value;
        Assert.NotNull(responseValue);

        var availableProperty = responseValue.GetType().GetProperty("available");
        Assert.NotNull(availableProperty);
        var available = (bool?)availableProperty.GetValue(responseValue);
        Assert.True(available);

        var messageProperty = responseValue.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(responseValue) as string;
        Assert.Equal("Nickname is available", message);
    }

    // 닉네임 중복 확인 - 존재하는 닉네임
    [Fact]
    public async Task CheckNickname_ShouldReturnOk_WhenNicknameIsNotAvailable()
    {
        // Given
        var nickname = "takennick";
        _userServiceMock
            .Setup(x => x.IsNicknameAvailableAsync(nickname))
            .ReturnsAsync(false);

        // When
        var result = await _controller.CheckNickname(nickname);

        // Then
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseValue = okResult.Value;
        Assert.NotNull(responseValue);

        var availableProperty = responseValue.GetType().GetProperty("available");
        Assert.NotNull(availableProperty);
        var available = (bool?)availableProperty.GetValue(responseValue);
        Assert.False(available);

        var messageProperty = responseValue.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(responseValue) as string;
        Assert.Equal("Nickname already exists", message);
    }

    // 닉네임 중복 확인 - 빈 문자열이 들어왔을 때
    [Fact]
    public async Task CheckNickname_ShouldReturnBadRequest_WhenNicknameIsEmpty()
    {
        // When
        var result = await _controller.CheckNickname("");

        // Then
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var responseValue = badRequestResult.Value;
        Assert.NotNull(responseValue);

        var messageProperty = responseValue.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(responseValue) as string;
        Assert.Equal("Nickname is required", message);
    }

    // 닉네임 중복 확인 - null이 들어왔을 때
    [Fact]
    public async Task CheckNickname_ShouldReturnBadRequest_WhenNicknameIsNull()
    {
        // When
        var result = await _controller.CheckNickname(null!);

        // Then
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var responseValue = badRequestResult.Value;
        Assert.NotNull(responseValue);

        var messageProperty = responseValue.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(responseValue) as string;
        Assert.Equal("Nickname is required", message);
    }

    #endregion
}
