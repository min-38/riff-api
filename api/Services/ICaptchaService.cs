namespace api.Services;

public interface ICaptchaService
{
    Task<bool> VerifyTurnstileTokenAsync(string token, string? remoteIp = null);
}
