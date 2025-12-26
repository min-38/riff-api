using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using api.Services;
using DotNetEnv;

namespace api.Tests.Services;

public class CaptchaServiceTests
{
    private readonly Mock<ILogger<CaptchaService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly string _testSecretKey = "1x0000000000000000000000000000000AA";

    public CaptchaServiceTests()
    {
        Env.Load();

        _loggerMock = new Mock<ILogger<CaptchaService>>();
        _configurationMock = new Mock<IConfiguration>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();

        // env 파일에서 TURNSTILE_SECRET_KEY 가져오기
        Environment.SetEnvironmentVariable("TURNSTILE_SECRET_KEY", _testSecretKey);
    }

    // Captcha 인증 성공
    [Fact]
    public async Task VerifyTurnstileTokenAsync_ValidToken_ReturnsTrue()
    {
        // Arrange
        var successResponse = new
        {
            success = true,
            challenge_ts = "2024-12-26T00:00:00.000Z",
            hostname = "localhost"
        };

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), // 어떤 HTTP 요청이든 매칭
                ItExpr.IsAny<CancellationToken>() // 어떤 취소 토큰이든 매칭
            )
            .ReturnsAsync(new HttpResponseMessage // OK 응답 반환
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(successResponse))
            });

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var captchaService = new CaptchaService(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _configurationMock.Object
        );

        // Act
        var result = await captchaService.VerifyTurnstileTokenAsync("valid-token", "127.0.0.1");

        // Assert
        Assert.True(result);
    }

    // 유효하지 않은 토큰으로 인증 실패
    [Fact]
    public async Task VerifyTurnstileTokenAsync_InvalidToken_ReturnsFalse()
    {
        // Arrange
        var failureResponse = new
        {
            success = false,
            // ReSharper disable once StringLiteralTypo
            error_codes = new[] { "invalid-input-response" }
        };

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(failureResponse))
            });

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var captchaService = new CaptchaService(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _configurationMock.Object
        );

        // Act
        var result = await captchaService.VerifyTurnstileTokenAsync("invalid-token", "127.0.0.1");

        // Assert
        Assert.False(result);
    }

    // 토큰이 null 또는 빈 문자열인 경우 인증 실패
    [Fact]
    public async Task VerifyTurnstileTokenAsync_NullOrEmptyToken_ReturnsFalse()
    {
        // Arrange
        var captchaService = new CaptchaService(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _configurationMock.Object
        );

        // Act
        var resultNull = await captchaService.VerifyTurnstileTokenAsync(null!, "127.0.0.1");
        var resultEmpty = await captchaService.VerifyTurnstileTokenAsync("", "127.0.0.1");

        // Assert
        Assert.False(resultNull);
        Assert.False(resultEmpty);
    }

    // HTTP 요청 중 예외 발생 시 인증 실패
    [Fact]
    public async Task VerifyTurnstileTokenAsync_HttpException_ReturnsFalse()
    {
        // Arrange
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var captchaService = new CaptchaService(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _configurationMock.Object
        );

        // Act
        var result = await captchaService.VerifyTurnstileTokenAsync("token", "127.0.0.1");

        // Assert
        Assert.False(result);
    }

    // 올바른 HTTP 요청이 전송되는지 검증
    [Fact]
    public async Task VerifyTurnstileTokenAsync_SendsCorrectRequest()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var successResponse = new
        {
            success = true
        };

        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(successResponse))
            });

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var captchaService = new CaptchaService(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _configurationMock.Object
        );

        // Act
        await captchaService.VerifyTurnstileTokenAsync("test-token", "192.168.1.1");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://challenges.cloudflare.com/turnstile/v0/siteverify", capturedRequest.RequestUri?.ToString());

        var content = await capturedRequest.Content!.ReadAsStringAsync();
        Assert.Contains("secret=" + Uri.EscapeDataString(_testSecretKey), content);
        Assert.Contains("response=test-token", content);
        Assert.Contains("remoteip=192.168.1.1", content);
    }
}
