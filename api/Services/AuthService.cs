using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using api.Data;
using api.Models;
using api.DTOs.Requests;
using api.DTOs.Responses;
using api.Exceptions;
using api.Utils;
using BCrypt.Net;

namespace api.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuthService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IUserService _userService;
    private readonly IEmailService _emailService;
    private readonly ITokenService _tokenService;
    private readonly IRedisService _redisService;
    private readonly ICaptchaService _captchaService;

    public AuthService
    (
        ApplicationDbContext context,
        ILogger<AuthService> logger,
        IConfiguration configuration,
        IUserService userService,
        IEmailService emailService,
        ITokenService tokenService,
        IRedisService redisService,
        ICaptchaService captchaService
    )
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _userService = userService;
        _emailService = emailService;
        _tokenService = tokenService;
        _redisService = redisService;
        _captchaService = captchaService;
    }

    // 회원가입 처리
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        // 이메일을 소문자로 정규화
        request.Email = request.Email.ToLower();

        // 차단된 이메일인지 체크
        var blockedByEmail = await _context.BlockedUsers
            .FirstOrDefaultAsync(bu => bu.Email == request.Email && (bu.ExpiresAt == null || bu.ExpiresAt > DateTime.UtcNow));

        if (blockedByEmail != null)
        {
            var reason = string.IsNullOrEmpty(blockedByEmail.Reason)
                ? "This email has been blocked"
                : blockedByEmail.Reason;

            if (blockedByEmail.ExpiresAt.HasValue)
                reason += $". Access will be restored at {blockedByEmail.ExpiresAt.Value:yyyy-MM-dd HH:mm:ss} UTC";

            throw new InvalidOperationException(reason);
        }

        // 이메일 중복 확인
        var existingUser = await _userService.GetUserByEmailAsync(request.Email);
        if (existingUser != null)
        {
            // 미인증 계정이고 만료된 경우 삭제
            if (!existingUser.Verified && existingUser.EmailVerificationExpiry.HasValue &&
                existingUser.EmailVerificationExpiry.Value < DateTime.UtcNow)
            {
                _context.Users.Remove(existingUser);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Expired unverified account deleted: {Email}", request.Email);
            }
            else
            {
                throw new InvalidOperationException("Email already exists");
            }
        }

        // 닉네임 중복 체크
        if (await _userService.GetUserByNicknameAsync(request.Nickname) != null)
            throw new InvalidOperationException("Nickname already exists");

        // 약관 동의 검증
        if (!request.TermsOfServiceAgreed)
            throw new InvalidOperationException("Terms of service agreement is required");

        if (!request.PrivacyPolicyAgreed)
            throw new InvalidOperationException("Privacy policy agreement is required");

        // 비밀번호 해싱
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // 이메일 인증 토큰 생성 (24시간 유효)
        var verificationToken = SecurityHelper.GenerateVerificationToken();
        var verificationExpiry = DateTime.UtcNow.AddHours(24);

        // 새로운 User 생성
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = request.Email,
            Password = hashedPassword,
            Nickname = request.Nickname,
            Phone = request.Phone,
            Verified = false, // 초기값은 미인증
            EmailVerificationToken = verificationToken,
            EmailVerificationExpiry = verificationExpiry,
            Rating = 0.0,
            TermsOfServiceAgreed = request.TermsOfServiceAgreed,
            PrivacyPolicyAgreed = request.PrivacyPolicyAgreed,
            MarketingAgreed = request.MarketingAgreed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        // UserOAuth 테이블에 이메일 provider 추가
        var userOAuth = new UserOAuth
        {
            UserId = user.Id,
            Provider = "email",
            ProviderId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.UserOAuths.Add(userOAuth);

        await _context.SaveChangesAsync();

        // 이메일 인증 링크 발송
        await _emailService.SendVerificationLinkAsync(user.Email, verificationToken);

        _logger.LogInformation("User registered (unverified): {Email}", user.Email);

        // 회원가입 성공 응답
        return new RegisterResponse
        {
            Message = "Registration successful. Please check your email to verify your account.",
            Email = user.Email,
            VerificationToken = verificationToken // 인증 토큰도 같이 전달
        };
    }

    // 이메일 토큰으로 인증 및 자동 로그인
    public async Task<AuthResponse> VerifyEmailByTokenAsync(string token)
    {
        // 토큰으로 사용자 조회 및 검증
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.EmailVerificationToken == token &&
                                      u.Verified == false &&
                                      u.EmailVerificationExpiry > DateTime.UtcNow);
        
        if (user == null) throw new InvalidOperationException("Invalid or expired verification link");

        // 사용자 인증 완료
        user.Verified = true;
        user.EmailVerificationToken = null; // 토큰 재사용 방지
        user.EmailVerificationExpiry = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Email verified for user: {Email}", user.Email);

        // JWT 토큰 생성
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id);

        // JWT 만료 시간 가져오기
        var expirationMinutesStr = Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES") ?? "60";
        var expirationMinutes = int.Parse(expirationMinutesStr);

        // AuthResponse 반환하여 자동 로그인 처리
        return new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            Nickname = user.Nickname,
            Verified = user.Verified,
            Token = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes)
        };
    }

    // 로그인
    public async Task<AuthResponse> LogInAsync(LoginRequest request)
    {
        // 이메일을 소문자로 정규화
        request.Email = request.Email.ToLower();

        // 이메일로 사용자 조회
        var user = await _userService.GetUserByEmailAsync(request.Email);

        // 타이밍 공격 방지: 이메일이 없을 때도 BCrypt 검증을 수행하여 응답 시간을 비슷하게 유지
        // 가짜 해시를 사용하여 실제 검증과 동일한 시간 소요
        var passwordToVerify = user?.Password ?? "$2a$11$InvalidHashToPreventTimingAttack1234567890123456";
        var isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, passwordToVerify);

        // 이메일 없음/비밀번호 틀림/미인증 모두 동일한 에러 메시지
        if (user == null || !isPasswordValid || !user.Verified)
            throw new InvalidOperationException("Invalid credentials");

        // 차단된 유저인지 체크 (user_id로 차단된 경우 또는 email로 차단된 경우)
        var blockedUser = await _context.BlockedUsers
            .FirstOrDefaultAsync(bu =>
                (bu.UserId == user.Id || bu.Email == user.Email) &&
                (bu.ExpiresAt == null || bu.ExpiresAt > DateTime.UtcNow));

        if (blockedUser != null)
        {
            var reason = string.IsNullOrEmpty(blockedUser.Reason)
                ? "This account has been blocked"
                : blockedUser.Reason;

            // 일시적 차단이면 만료 시간 추가
            if (blockedUser.ExpiresAt.HasValue)
                reason += $". Access will be restored at {blockedUser.ExpiresAt.Value:yyyy-MM-dd HH:mm:ss} UTC";

            throw new InvalidOperationException(reason);
        }

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

    // 인증 토큰으로 인증 정보 조회
    public async Task<VerificationInfoResponse> GetVerificationInfoAsync(string verificationToken)
    {
        // 토큰으로 사용자 조회
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.EmailVerificationToken == verificationToken &&
                                      u.Verified == false &&
                                      u.EmailVerificationExpiry > DateTime.UtcNow);

        // 토큰 검증 실패
        if (user == null)
            throw new InvalidOperationException("Invalid or expired verification token");

        // 이메일과 전송 시간 반환
        return new VerificationInfoResponse
        {
            Email = user.Email,
            SentAt = user.CreatedAt
        };
    }

    // 인증 이메일 재전송
    public async Task<ResendVerificationResponse> ResendVerificationEmailAsync(string verificationToken, string? captchaToken = null)
    {
        // 토큰으로 사용자 조회
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.EmailVerificationToken == verificationToken &&
                                      u.Verified == false &&
                                      u.EmailVerificationExpiry > DateTime.UtcNow);

        // 토큰 검증 실패
        if (user == null)
            throw new InvalidOperationException("Invalid or expired verification token");

        // -- Rate Limiting 체크 --
        // 일일 제한 체크 (24시간에 15번)
        var dailyRateLimitKey = $"resend_email_daily:{user.Email}";
        var dailyAttemptCount = await _redisService.IncrementAsync(dailyRateLimitKey, TimeSpan.FromHours(24));
        if (dailyAttemptCount > 15)
        {
            _logger.LogWarning("Daily rate limit exceeded for email resend: {Email} (Attempt {Count})", user.Email, dailyAttemptCount);
            throw new RateLimitException("Too many resend attempts today. Please try again tomorrow.", 86400); // 24시간 = 86400초
        }

        // 시간당 제한 체크 (1시간에 5번)
        var hourlyRateLimitKey = $"resend_email_hourly:{user.Email}";
        var hourlyAttemptCount = await _redisService.IncrementAsync(hourlyRateLimitKey, TimeSpan.FromHours(1));
        if (hourlyAttemptCount > 5)
        {
            _logger.LogWarning("Hourly rate limit exceeded for email resend: {Email} (Attempt {Count})", user.Email, hourlyAttemptCount);
            throw new RateLimitException("Too many resend attempts. Please try again in 1 hour.", 3600); // 1시간 = 3600초
        }

        // 3번 이상 시도 시 CAPTCHA 체크 필수
        if (hourlyAttemptCount >= 3)
        {
            if (string.IsNullOrEmpty(captchaToken))
            {
                _logger.LogWarning("CAPTCHA required but not provided: {Email} (Attempt {Count})", user.Email, hourlyAttemptCount);
                throw new InvalidOperationException("CAPTCHA verification is required after 3 attempts");
            }

            var isCaptchaValid = await _captchaService.VerifyTurnstileTokenAsync(captchaToken);
            if (!isCaptchaValid)
            {
                _logger.LogWarning("CAPTCHA verification failed: {Email}", user.Email);
                throw new InvalidOperationException("CAPTCHA verification failed");
            }

            _logger.LogInformation("CAPTCHA verification successful: {Email}", user.Email);
        }

        // 만료 시간 연장 (24시간)
        // 토큰은 변경하지 않음
        user.EmailVerificationExpiry = DateTime.UtcNow.AddHours(24);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // 인증 이메일 재전송 (기존 토큰 사용)
        await _emailService.SendVerificationLinkAsync(user.Email, user.EmailVerificationToken!);

        _logger.LogInformation("Verification email resent to: {Email} (Hourly: {Hourly}/5, Daily: {Daily}/15)",
            user.Email, hourlyAttemptCount, dailyAttemptCount);

        // 성공 응답
        return new ResendVerificationResponse
        {
            Message = "Verification email has been resent. Please check your email."
        };
    }
}