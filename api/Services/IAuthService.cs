using api.DTOs.Requests;
using api.DTOs.Responses;
using api.Models;

namespace api.Services;

public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request);
    Task<VerificationInfoResponse> GetVerificationInfoAsync(string verificationToken);
    Task<ResendVerificationResponse> ResendVerificationEmailAsync(string verificationToken, string? captchaToken = null);
    Task<AuthResponse> VerifyEmailByTokenAsync(string token);
    Task<AuthResponse> LogInAsync(LoginRequest request);
    Task<bool> LogOutAsync(string? refreshToken = null);
    Task<AuthResponse> RefreshAccessTokenAsync(string refreshToken);
}
