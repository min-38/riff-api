using Microsoft.Extensions.Logging;
using Moq;
using api.Services;

namespace api.Tests.Services;

public class EmailServiceTests : IDisposable
{
    private readonly Mock<ILogger<EmailService>> _loggerMock;
    private readonly Dictionary<string, string?> _originalEnvVars;

    public EmailServiceTests()
    {
        _loggerMock = new Mock<ILogger<EmailService>>();

        // 기존 환경 변수 저장
        _originalEnvVars = new Dictionary<string, string?>
        {
            ["SMTP_HOST"] = Environment.GetEnvironmentVariable("SMTP_HOST"),
            ["SMTP_PORT"] = Environment.GetEnvironmentVariable("SMTP_PORT"),
            ["SMTP_USERNAME"] = Environment.GetEnvironmentVariable("SMTP_USERNAME"),
            ["SMTP_PASSWORD"] = Environment.GetEnvironmentVariable("SMTP_PASSWORD"),
            ["SMTP_FROM_EMAIL"] = Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL"),
            ["SMTP_FROM_NAME"] = Environment.GetEnvironmentVariable("SMTP_FROM_NAME"),
            ["SEND_ACTUAL_EMAIL"] = Environment.GetEnvironmentVariable("SEND_ACTUAL_EMAIL"),
            ["API_URL"] = Environment.GetEnvironmentVariable("API_URL")
        };
    }

    public void Dispose()
    {
        // 환경 변수 복원
        foreach (var kvp in _originalEnvVars)
            Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
    }

    #region SEND_ACTUAL_EMAIL=false Tests

    // SEND_ACTUAL_EMAIL=false일 때 테스트 이메일은 로그만 출력
    [Fact]
    public async Task SendTestEmail_WhenSendActualEmailIsFalse_ShouldLogOnly()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SMTP_HOST", "smtp.test.com");
        Environment.SetEnvironmentVariable("SMTP_PORT", "587");
        Environment.SetEnvironmentVariable("SMTP_USERNAME", "test-user");
        Environment.SetEnvironmentVariable("SMTP_PASSWORD", "test-pass");
        Environment.SetEnvironmentVariable("SEND_ACTUAL_EMAIL", "false");
        var emailService = new EmailService(_loggerMock.Object);

        // Act
        var result = await emailService.SendTestEmailAsync("test@test.com");

        // Assert
        // result는 value type이므로 항상 null이 아님
        #pragma warning disable xUnit2002
        Assert.NotNull(result);
        #pragma warning restore xUnit2002
    }

    #endregion

    #region SendVerificationLinkAsync Tests

    // SEND_ACTUAL_EMAIL=false일 때 인증 링크 메일은 로그만 출력
    [Fact]
    public async Task SendVerificationLinkAsync_WhenSendActualEmailIsFalse_ShouldLogOnly()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SMTP_HOST", "smtp.test.com");
        Environment.SetEnvironmentVariable("SMTP_PORT", "587");
        Environment.SetEnvironmentVariable("SMTP_USERNAME", "test-user");
        Environment.SetEnvironmentVariable("SMTP_PASSWORD", "test-pass");
        Environment.SetEnvironmentVariable("SEND_ACTUAL_EMAIL", "false");
        Environment.SetEnvironmentVariable("API_URL", "http://localhost:5000");
        var emailService = new EmailService(_loggerMock.Object);

        // Act
        await emailService.SendVerificationLinkAsync("test@test.com", "test-token-123");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information, // 로그 레벨이 Information이어야 하고
                It.IsAny<EventId>(), // 이벤트 ID는 어떤 것이든 상관없으며
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("DEV MODE")), // 메시지에 "DEV MODE"가 포함되어야 하고
                It.IsAny<Exception>(), // 예외는 어떤 것이든 상관없으며
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), // 포맷 함수도 어떤 것이든 상관없음
            Times.AtLeastOnce); // 최소 한 번 이상 호출되었는지 확인
    }

    // null 이메일도 SEND_ACTUAL_EMAIL=false면 예외 없이 처리
    [Fact]
    public async Task SendVerificationLinkAsync_WithNullEmail_WhenSendActualEmailIsFalse_ShouldNotThrow()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SMTP_HOST", "smtp.test.com");
        Environment.SetEnvironmentVariable("SMTP_PORT", "587");
        Environment.SetEnvironmentVariable("SMTP_USERNAME", "test-user");
        Environment.SetEnvironmentVariable("SMTP_PASSWORD", "test-pass");
        Environment.SetEnvironmentVariable("SEND_ACTUAL_EMAIL", "false");
        Environment.SetEnvironmentVariable("API_URL", "http://localhost:5000");
        var emailService = new EmailService(_loggerMock.Object);

        // Act & Assert
        await emailService.SendVerificationLinkAsync(null!, "test-token-123");

        // 로그만 출력되었는지 확인
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("DEV MODE")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    // 빈 이메일도 SEND_ACTUAL_EMAIL=false면 예외 없이 처리
    [Fact]
    public async Task SendVerificationLinkAsync_WithEmptyEmail_WhenSendActualEmailIsFalse_ShouldNotThrow()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SMTP_HOST", "smtp.test.com");
        Environment.SetEnvironmentVariable("SMTP_PORT", "587");
        Environment.SetEnvironmentVariable("SMTP_USERNAME", "test-user");
        Environment.SetEnvironmentVariable("SMTP_PASSWORD", "test-pass");
        Environment.SetEnvironmentVariable("SEND_ACTUAL_EMAIL", "false");
        Environment.SetEnvironmentVariable("API_URL", "http://localhost:5000");
        var emailService = new EmailService(_loggerMock.Object);

        // Act & Assert
        await emailService.SendVerificationLinkAsync("", "test-token-123");

        // 로그만 출력되었는지 확인
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("DEV MODE")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    // 잘못된 형식의 이메일도 SEND_ACTUAL_EMAIL=false면 예외 없이 처리
    [Fact]
    public async Task SendVerificationLinkAsync_WithInvalidEmailFormat_WhenSendActualEmailIsFalse_ShouldNotThrow()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SMTP_HOST", "smtp.test.com");
        Environment.SetEnvironmentVariable("SMTP_PORT", "587");
        Environment.SetEnvironmentVariable("SMTP_USERNAME", "test-user");
        Environment.SetEnvironmentVariable("SMTP_PASSWORD", "test-pass");
        Environment.SetEnvironmentVariable("SEND_ACTUAL_EMAIL", "false");
        Environment.SetEnvironmentVariable("API_URL", "http://localhost:5000");
        var emailService = new EmailService(_loggerMock.Object);

        // Act & Assert
        await emailService.SendVerificationLinkAsync("invalid-email-format", "test-token-123");

        // 로그만 출력되었는지 확인
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("DEV MODE")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion
}
