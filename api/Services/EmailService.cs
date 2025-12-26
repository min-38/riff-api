using api.Templates.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace api.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;

        // SMTP 설정 가져오기
        _smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST")
            ?? throw new InvalidOperationException("SMTP_HOST environment variable is not set");
        _smtpPort = int.Parse(Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587");
        _smtpUsername = Environment.GetEnvironmentVariable("SMTP_USERNAME")
            ?? throw new InvalidOperationException("SMTP_USERNAME environment variable is not set");
        _smtpPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD")
            ?? throw new InvalidOperationException("SMTP_PASSWORD environment variable is not set");

        _fromEmail = Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL") ?? "noreply@riff.com";
        _fromName = Environment.GetEnvironmentVariable("SMTP_FROM_NAME") ?? "Riff";
    }

    // SMTP 연결 테스트
    public async Task<(bool Success, string Message)> TestSmtpConnectionAsync()
    {
        try
        {
            _logger.LogInformation("Testing Oracle Cloud SMTP connection");

            // SMTP 설정 확인
            var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST");
            var smtpUsername = Environment.GetEnvironmentVariable("SMTP_USERNAME");

            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUsername))
                return (false, "SMTP environment variables are not properly configured");

            _logger.LogInformation("Oracle Cloud SMTP is configured - Host: {Host}, Username: {Username}",
                smtpHost, smtpUsername);

            return (true, $"Oracle Cloud SMTP connection configured successfully (Host: {smtpHost})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP connection test failed");
            return (false, $"SMTP connection failed: {ex.Message}");
        }
    }

    // 이메일 발송
    private async Task SendEmailAsync(string toEmail, IEmailTemplate template)
    {
        // SEND_ACTUAL_EMAIL 값 확인하여 실제 발송 여부 결정
        var sendActualEmail = bool.TryParse(Environment.GetEnvironmentVariable("SEND_ACTUAL_EMAIL"), out var result) && result;

        if (!sendActualEmail)
        {
            _logger.LogInformation("=== [DEV MODE] Email would be sent ===");
            _logger.LogInformation("To: {Email}", toEmail);
            _logger.LogInformation("Subject: {Subject}", template.Subject);
            _logger.LogInformation("Body: {Body}", template.GenerateHtml());
            _logger.LogInformation("=== Set SEND_ACTUAL_EMAIL=true in .env to send actual emails ===");
            await Task.CompletedTask;
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_fromName, _fromEmail));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = template.Subject;

            // Reply-To 헤더 추가 -> 스팸 점수 개선
            message.ReplyTo.Add(new MailboxAddress(_fromName, _fromEmail));

            // 추가 헤더 -> 스팸 필터 통과율 향상
            message.Headers.Add("X-Mailer", "Riff Email Service");
            message.Headers.Add("X-Priority", "3");
            message.Headers.Add("Importance", "Normal");
            message.Headers.Add("X-MSMail-Priority", "Normal");

            // 발신자 신뢰성 향상
            message.Headers.Add("List-Unsubscribe", $"<mailto:{_fromEmail}?subject=unsubscribe>");
            message.Headers.Add("Precedence", "bulk");

            // Message-ID 자동 생성
            // MailKit이 자동으로 생성하지만 명시적으로 추가
            // 이메일 추적 및 스레딩에 도움
            if (string.IsNullOrEmpty(message.MessageId))
                message.MessageId = MimeKit.Utils.MimeUtils.GenerateMessageId();

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = template.GenerateHtml(),
                TextBody = template.GeneratePlainText() // HTML과 일치하는 Plain text 버전
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();

            // Oracle Cloud SMTP 연결
            await client.ConnectAsync(_smtpHost, _smtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_smtpUsername, _smtpPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent successfully via Oracle Cloud SMTP to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            throw new InvalidOperationException("Failed to send email", ex);
        }
    }

    // 테스트 이메일 발송
    public async Task<(bool Success, string Message)> SendTestEmailAsync(string toEmail)
    {
        try
        {
            var htmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2>Oracle Cloud Email Delivery 연결 성공!</h2>
                    <p>Oracle Cloud SMTP 이메일 서비스가 정상적으로 작동하고 있습니다.</p>
                    <p><strong>발송 시간:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
                    <p><strong>이메일 서비스:</strong> Oracle Cloud Email Delivery</p>
                    <hr>
                    <p style='color: #999; font-size: 12px;'>이 메일은 Oracle Cloud SMTP 연결 테스트용 메일입니다.</p>
                </body>
                </html>
            ";

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_fromName, _fromEmail));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = "테스트 이메일 - Riff Oracle Cloud SMTP 연결 확인";

            // Reply-To 헤더 추가
            message.ReplyTo.Add(new MailboxAddress(_fromName, _fromEmail));

            // 추가 헤더
            message.Headers.Add("X-Mailer", "Riff Email Service");
            message.Headers.Add("X-Priority", "3");

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody,
                TextBody = "Oracle Cloud Email Delivery 연결 성공!\n\nOracle Cloud SMTP 이메일 서비스가 정상적으로 작동하고 있습니다."
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_smtpHost, _smtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_smtpUsername, _smtpPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Test email sent successfully to {Email} via Oracle Cloud SMTP", toEmail);
            return (true, $"Test email sent successfully to {toEmail}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test email to {Email}", toEmail);
            return (false, $"Failed to send test email: {ex.Message}");
        }
    }

    // 이메일 인증 링크 발송
    public async Task SendVerificationLinkAsync(string toEmail, string verificationToken)
    {
        var apiUrl = Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:5000";
        var verificationUrl = $"{apiUrl}/auth/verify-email/{verificationToken}";

        // 템플릿 생성
        var template = new VerificationLinkEmailTemplate(verificationUrl);

        // 이메일 전송
        await SendEmailAsync(toEmail, template);

        _logger.LogInformation("Verification link sent to {Email}", toEmail);
    }

    // TODO: 비밀번호 재설정 이메일 발송
    public async Task SendPasswordResetEmailAsync(string toEmail, string resetToken)
    {
        _logger.LogInformation("Password reset email would be sent to {Email}", toEmail);
        await Task.CompletedTask;
    }
}
