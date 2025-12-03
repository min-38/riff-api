using api.DTOs.Requests;
using api.DTOs.Responses;
using api.Models;

namespace api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);

    Task<AuthResponse> LogInAsync(LoginRequest request);
    Task<bool> LogOutAsync(string? refreshToken = null);
    Task<AuthResponse> RefreshAccessTokenAsync(string refreshToken);
}
