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
            ["SEND_ACTUAL_EMAIL"] = Environment.GetEnvironmentVariable("SEND_ACTUAL_EMAIL")
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
        Assert.NotNull(result);
    }

    // SEND_ACTUAL_EMAIL=false일 때 인증 메일은 로그만 출력
    [Fact]
    public async Task SendVerificationEmail_WhenSendActualEmailIsFalse_ShouldLogOnly()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SMTP_HOST", "smtp.test.com");
        Environment.SetEnvironmentVariable("SMTP_PORT", "587");
        Environment.SetEnvironmentVariable("SMTP_USERNAME", "test-user");
        Environment.SetEnvironmentVariable("SMTP_PASSWORD", "test-pass");
        Environment.SetEnvironmentVariable("SEND_ACTUAL_EMAIL", "false");
        var emailService = new EmailService(_loggerMock.Object);

        // Act
        await emailService.SendVerificationEmailAsync("test@test.com", "123456");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information, // 로그 레벨은 Information이어ㅑ 하고,
                It.IsAny<EventId>(),  // EventId는 상관없고
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("DEV MODE")), // 로그 메시지에 "DEV MODE"가 포함되어야 하고
                It.IsAny<Exception>(), // Exception은 상관없고
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), // formatter는 상관없음
            Times.AtLeastOnce); // 최소 1번 이상 호출되어야 함
    }

    // null 이메일도 SEND_ACTUAL_EMAIL=false면 예외 없이 처리
    [Fact]
    public async Task SendVerificationEmail_WithNullEmail_WhenSendActualEmailIsFalse_ShouldNotThrow()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SMTP_HOST", "smtp.test.com");
        Environment.SetEnvironmentVariable("SMTP_PORT", "587");
        Environment.SetEnvironmentVariable("SMTP_USERNAME", "test-user");
        Environment.SetEnvironmentVariable("SMTP_PASSWORD", "test-pass");
        Environment.SetEnvironmentVariable("SEND_ACTUAL_EMAIL", "false");
        var emailService = new EmailService(_loggerMock.Object);

        // Act & Assert - 개발 모드에서는 실제 발송을 안 하므로 예외가 발생하지 않음
        await emailService.SendVerificationEmailAsync(null!, "123456");

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
    public async Task SendVerificationEmail_WithEmptyEmail_WhenSendActualEmailIsFalse_ShouldNotThrow()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SMTP_HOST", "smtp.test.com");
        Environment.SetEnvironmentVariable("SMTP_PORT", "587");
        Environment.SetEnvironmentVariable("SMTP_USERNAME", "test-user");
        Environment.SetEnvironmentVariable("SMTP_PASSWORD", "test-pass");
        Environment.SetEnvironmentVariable("SEND_ACTUAL_EMAIL", "false");
        var emailService = new EmailService(_loggerMock.Object);

        // Act & Assert - 개발 모드에서는 실제 발송을 안 하므로 예외가 발생하지 않음
        await emailService.SendVerificationEmailAsync("", "123456");

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
    public async Task SendVerificationEmail_WithInvalidEmailFormat_WhenSendActualEmailIsFalse_ShouldNotThrow()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SMTP_HOST", "smtp.test.com");
        Environment.SetEnvironmentVariable("SMTP_PORT", "587");
        Environment.SetEnvironmentVariable("SMTP_USERNAME", "test-user");
        Environment.SetEnvironmentVariable("SMTP_PASSWORD", "test-pass");
        Environment.SetEnvironmentVariable("SEND_ACTUAL_EMAIL", "false");
        var emailService = new EmailService(_loggerMock.Object);

        // Act & Assert - 개발 모드에서는 실제 발송을 안 하므로 예외가 발생하지 않음
        await emailService.SendVerificationEmailAsync("invalid-email-format", "123456");

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
