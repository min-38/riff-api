namespace api.Services;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string verificationCode);
    Task<(bool Success, string Message)> TestSmtpConnectionAsync();
    Task<(bool Success, string Message)> SendTestEmailAsync(string toEmail);
    Task SendPasswordResetEmailAsync(string toEmail, string resetToken);
}
