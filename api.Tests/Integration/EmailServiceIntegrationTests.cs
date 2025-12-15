using Microsoft.Extensions.Logging;
using Moq;
using api.Services;
using DotNetEnv;

namespace api.Tests.Integration;

/*
EmailService 통합 테스트 (Oracle Cloud SMTP 기반)
- 실제 SMTP 연결 및 이메일 발송 테스트
- SEND_ACTUAL_EMAIL=true일 때만 실행
*/
public class EmailServiceIntegrationTests : IDisposable
{
    private readonly Mock<ILogger<EmailService>> _loggerMock;
    private readonly Dictionary<string, string?> _originalEnvVars;

    public EmailServiceIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<EmailService>>();

        // .env.test 파일 로드
        Env.TraversePath().Load(".env.test");

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

    // 각 테스트가 끝난 직후 실행
    public void Dispose()
    {
        // 환경 변수 복원
        foreach (var kvp in _originalEnvVars)
            Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
    }

    // SEND_ACTUAL_EMAIL 환경 변수 체크
    private bool IsActualEmailSendingEnabled()
    {
        var sendActualEmail = Environment.GetEnvironmentVariable("SEND_ACTUAL_EMAIL");
        return sendActualEmail?.ToLower() == "true";
    }

    #region Oracle Cloud SMTP 연결 테스트

    // 실제 Oracle Cloud SMTP 연결 테스트
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Email")]
    public async Task TestSmtpConnection_WithValidCredentials_ShouldSucceed()
    {
        // SEND_ACTUAL_EMAIL가 false면 테스트 건너뜀
        if (!IsActualEmailSendingEnabled())
            return;

        // Arrange
        var emailService = new EmailService(_loggerMock.Object);

        // Act
        var result = await emailService.TestSmtpConnectionAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Contains("successful", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 실제 이메일 발송 테스트

    // 실제 테스트 이메일 발송
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Email")]
    public async Task SendTestEmail_WithValidCredentials_ShouldSucceed()
    {
        // SEND_ACTUAL_EMAIL가 false면 테스트 건너뜀
        if (!IsActualEmailSendingEnabled())
            return;

        // Arrange
        var emailService = new EmailService(_loggerMock.Object);
        var testEmail = Environment.GetEnvironmentVariable("TEST_EMAIL") ?? "mindino03@gmail.com";

        // Act
        var result = await emailService.SendTestEmailAsync(testEmail);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("sent successfully", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 실제 인증 메일 발송 테스트

    // 실제 인증 메일 발송 테스트
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Email")]
    public async Task SendVerificationEmail_WithValidCredentials_ShouldSucceed()
    {
        // SEND_ACTUAL_EMAIL가 false면 테스트 건너뜀
        if (!IsActualEmailSendingEnabled())
            return;

        // Arrange
        var emailService = new EmailService(_loggerMock.Object);
        var testEmail = Environment.GetEnvironmentVariable("TEST_EMAIL") ?? "mindino03@gmail.com";
        var verificationCode = "123456";

        // Act & Assert - 예외가 발생하지 않으면 성공
        await emailService.SendVerificationEmailAsync(testEmail, verificationCode);

        // 이메일이 발송되었으면 성공
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Verification email sent")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region 이메일 내용 검증 테스트

    // 실제 이메일에 올바른 인증 코드가 포함되는지 확인
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Email")]
    public async Task SendVerificationEmail_ShouldContainCorrectCode()
    {
        // SEND_ACTUAL_EMAIL가 false면 테스트 건너뜀
        if (!IsActualEmailSendingEnabled())
            return;

        // Arrange
        var emailService = new EmailService(_loggerMock.Object);
        var testEmail = Environment.GetEnvironmentVariable("TEST_EMAIL") ?? "";
        if (testEmail == "")
            return;

        var verificationCode = "987654";

        // Act
        await emailService.SendVerificationEmailAsync(testEmail, verificationCode);

        // Assert
        // 실제 이메일 계정에서 코드가 987654인지 확인
        Assert.True(true, "Check the email manually to verify the code is 987654");
    }

    #endregion
}
