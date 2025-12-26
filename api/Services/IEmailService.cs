namespace api.Services;

public interface IEmailService
{
    Task SendVerificationLinkAsync(string toEmail, string verificationToken);
    Task<(bool Success, string Message)> TestSmtpConnectionAsync();
    Task<(bool Success, string Message)> SendTestEmailAsync(string toEmail);
    Task SendPasswordResetEmailAsync(string toEmail, string resetToken);
}
