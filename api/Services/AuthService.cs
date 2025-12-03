using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using api.Data;
using api.Models;
using api.DTOs.Requests;
using api.DTOs.Responses;
using BCrypt.Net;
using FirebaseAdmin.Auth;

namespace api.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuthService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IUserService _userService;
    private readonly IFirebaseService _firebaseService;
    private readonly ITokenService _tokenService;

    public AuthService
    (
        ApplicationDbContext context,
        ILogger<AuthService> logger,
        IConfiguration configuration,
        IUserService userService,
        IFirebaseService firebaseService,
        ITokenService tokenService
    )
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _userService = userService;
        _firebaseService = firebaseService;
        _tokenService = tokenService;
    }

    // 회원가입
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // Firebase ID Token 검증
        FirebaseToken? decodedToken;
        try
        {
            decodedToken = await _firebaseService.VerifyIdTokenAsync(request.FirebaseIdToken);
            if (decodedToken?.Claims?.TryGetValue("phone_number", out var phoneObj) == true)
            {
                var tokenPhone = phoneObj.ToString();
                if (tokenPhone != request.Phone)
                    throw new InvalidOperationException("Phone number does not match");
            }
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Invalid Firebase ID token");
        }

        // 이메일 중복 체크
        if (await _userService.GetUserByEmailAsync(request.Email) != null)
            throw new InvalidOperationException("Email already exists");

        // 닉네임 중복 체크
        if (await _userService.GetUserByNicknameAsync(request.Nickname) != null)
            throw new InvalidOperationException("Nickname already exists");

        // 비밀번호 해싱
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // User 엔티티 생성
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            Password = hashedPassword,
            Nickname = request.Nickname,
            Phone = request.Phone,
            Verified = true, // Firebase로 인증
            Rating = 0.0,
            VerificationToken = null,
            VerificationTokenExpiry = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // User 테이블에 추가
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User registered: {Email}, {Phone}", user.Email, user.Phone);

        return new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            Nickname = user.Nickname,
            Verified = user.Verified,
        };
    }

    // 로그인
    public async Task<AuthResponse> LogInAsync(LoginRequest request)
    {
        // 이메일로 사용자 조회
        var user = await _userService.GetUserByEmailAsync(request.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            throw new InvalidOperationException("Email or Password is not correct");

        if (!user.Verified)
            throw new InvalidOperationException("Account is not verified");

        // JWT 토큰 생성
        var token = _tokenService.GenerateAccessToken(user);

        // Refresh Token 생성
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id);

        // JWT 만료 시간 가져오기
        var expirationMinutesStr = Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES") ?? "60";
        var expirationMinutes = int.Parse(expirationMinutesStr);

        // AuthResponse 반환
        return new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            Nickname = user.Nickname,
            Verified = user.Verified,
            Token = token,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes)
        };
    }

    // 로그아웃
    public async Task<bool> LogOutAsync(string? refreshToken = null)
    {
        // Refresh Token이 제공된 경우 무효화
        if (!string.IsNullOrEmpty(refreshToken))
        {
            var token = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

            if (token != null && token.RevokedAt == null)
            {
                token.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Refresh token revoked for user {UserId}", token.UserId);
            }
        }

        _logger.LogInformation("User logged out");
        return true;
    }

    // 토큰 갱신
    public async Task<AuthResponse> RefreshAccessTokenAsync(string refreshToken)
    {
        var (accessToken, newRefreshToken, user) = await _tokenService.RefreshAccessTokenAsync(refreshToken);

        // JWT 만료 시간 가져오기
        var expirationMinutesStr = Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES") ?? "60";
        var expirationMinutes = int.Parse(expirationMinutesStr);

        return new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            Nickname = user.Nickname,
            Verified = user.Verified,
            Token = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes)
        };
    }
}