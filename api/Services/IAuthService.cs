using api.DTOs.Requests;
using api.DTOs.Responses;
using api.Models;

namespace api.Services;

public interface IAuthService
{
    // 이메일 인증 요청
    Task<bool> SendVerificationCodeAsync(string email);

    // 이메일 인증 확인 - 세션 토큰 반환
    Task<string> VerifyEmailCodeAsync(string email, string code);

    // 닉네임 중복 체크
    Task<bool> CheckNicknameAvailabilityAsync(string nickname);

    // 회원가입 (인증된 이메일 + 닉네임 + 패스워드)
    Task<AuthResponse> RegisterAsync(RegisterRequest request);

    Task<AuthResponse> LogInAsync(LoginRequest request);
    Task<bool> LogOutAsync(string? refreshToken = null);
    Task<AuthResponse> RefreshAccessTokenAsync(string refreshToken);
}
