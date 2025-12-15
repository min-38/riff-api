using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using api.Data;
using api.Models;
using api.DTOs.Requests;
using api.DTOs.Responses;
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

    public AuthService
    (
        ApplicationDbContext context,
        ILogger<AuthService> logger,
        IConfiguration configuration,
        IUserService userService,
        IEmailService emailService,
        ITokenService tokenService
    )
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _userService = userService;
        _emailService = emailService;
        _tokenService = tokenService;
    }

    // 이메일 인증 요청
    public async Task<bool> SendVerificationCodeAsync(string email)
    {
        // 이메일을 소문자로 정규화
        email = email.ToLower();

        // 1. 차단된 이메일인지 체크
        var blockedByEmail = await _context.BlockedUsers
            .FirstOrDefaultAsync(bu => bu.Email == email && (bu.ExpiresAt == null || bu.ExpiresAt > DateTime.UtcNow));

        if (blockedByEmail != null)
        {
            var reason = string.IsNullOrEmpty(blockedByEmail.Reason)
                ? "This email has been blocked"
                : blockedByEmail.Reason;
            throw new InvalidOperationException(reason);
        }

        // 2. 이미 인증된 이메일인지 체크
        var existingUser = await _userService.GetUserByEmailAsync(email);
        if (existingUser != null && existingUser.Verified)
            throw new InvalidOperationException("Email already exists");

        // 3. 미인증 유저가 있으면 재사용, 없으면 새로 생성
        User user;
        if (existingUser != null && !existingUser.Verified)
        {
            // 기존 미인증 유저 재사용
            user = existingUser;
            _logger.LogInformation("Reusing unverified user for verification: {Email}", email);
        }
        else
        {
            // 새 유저 생성 (임시)
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Password = "", // 아직 설정 안됨
                Nickname = "", // 아직 설정 안됨
                Phone = null,
                Verified = false,
                Rating = 0.0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
        }

        // 마지막 발송 후 5분 이내면 거부 (Rate Limiting)
        if (user.LastVerificationEmailSentAt.HasValue)
        {
            var timeSinceLastEmail = DateTime.UtcNow - user.LastVerificationEmailSentAt.Value;
            if (timeSinceLastEmail.TotalMinutes < 5)
            {
                var remainingSeconds = (int)(300 - timeSinceLastEmail.TotalSeconds);
                var remainingMinutes = remainingSeconds / 60;
                var remainingSecondsOnly = remainingSeconds % 60;
                throw new InvalidOperationException($"Please wait {remainingMinutes}m {remainingSecondsOnly}s before requesting another verification email");
            }
        }

        // 5번 시도 초과 시 차단
        if (user.VerificationEmailAttempts >= 5)
        {
            // 이메일을 차단 목록에 추가 (24시간 차단)
            var newBlockedUser = new BlockedUser
            {
                UserId = null, // 이메일 자체를 차단
                Email = email,
                Reason = "Too many verification attempts",
                BlockedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                BlockedBy = "system"
            };
            _context.BlockedUsers.Add(newBlockedUser);
            await _context.SaveChangesAsync();

            _logger.LogWarning("Email {Email} has been blocked due to too many verification attempts", email);
            throw new InvalidOperationException("Maximum verification email attempts exceeded. This email has been temporarily blocked for 24 hours.");
        }

        // 인증 토큰 및 코드 생성
        var verificationToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var verificationCode = GenerateVerificationCode();

        user.VerificationToken = verificationToken;
        user.VerificationCode = verificationCode;
        user.VerificationTokenExpiry = DateTime.UtcNow.AddHours(24);
        user.LastVerificationEmailSentAt = DateTime.UtcNow;
        user.VerificationEmailAttempts = user.VerificationEmailAttempts + 1;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // 이메일 발송
        await _emailService.SendVerificationEmailAsync(user.Email, verificationCode);
        _logger.LogInformation("Verification email sent to {Email} (Attempt {Attempts}/5)", user.Email, user.VerificationEmailAttempts);

        return true;
    }

    // 2단계: 이메일 인증 확인
    public async Task<string> VerifyEmailCodeAsync(string email, string code)
    {
        // 이메일을 소문자로 정규화
        email = email.ToLower();

        var user = await _userService.GetUserByEmailAsync(email);

        if (user == null)
            throw new InvalidOperationException("User not found");

        if (user.Verified)
            throw new InvalidOperationException("Email already verified");

        if (user.VerificationCode != code)
            throw new InvalidOperationException("Invalid verification code");

        if (user.VerificationTokenExpiry < DateTime.UtcNow)
            throw new InvalidOperationException("Verification code has expired");

        // 이메일 인증 완료 + 일회용 세션 토큰 생성 (30분 유효)
        var sessionToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        user.Verified = true;
        user.VerificationToken = null;
        user.VerificationCode = null;
        user.VerificationTokenExpiry = null;
        user.VerificationEmailAttempts = 0;
        user.RegistrationSessionToken = sessionToken;
        user.RegistrationSessionExpiry = DateTime.UtcNow.AddMinutes(30);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Email verified for user {Email}, session token generated", user.Email);
        return sessionToken;
    }

    // 3단계: 닉네임 중복 체크
    public async Task<bool> CheckNicknameAvailabilityAsync(string nickname)
    {
        var existingUser = await _userService.GetUserByNicknameAsync(nickname);
        return existingUser == null; // null이면 사용 가능
    }

    // 4단계: 회원가입 완료
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // 이메일을 소문자로 정규화
        request.Email = request.Email.ToLower();

        // 이메일로 유저 조회
        var user = await _userService.GetUserByEmailAsync(request.Email);

        if (user == null)
            throw new InvalidOperationException("Email not found. Please verify your email first.");

        if (!user.Verified)
            throw new InvalidOperationException("Email is not verified. Please verify your email first.");

        // 세션 토큰 검증 (이메일과 토큰이 매칭되어야 함)
        if (string.IsNullOrEmpty(user.RegistrationSessionToken))
            throw new InvalidOperationException("Invalid session. Please verify your email again.");

        // 중요: 요청한 이메일의 유저가 가진 세션 토큰과 요청 토큰이 일치해야 함
        // 다른 이메일로 변경해서 요청하면 해당 이메일 유저의 토큰과 비교되므로 실패
        if (user.RegistrationSessionToken != request.SessionToken)
            throw new InvalidOperationException("Invalid session token.");

        if (user.RegistrationSessionExpiry < DateTime.UtcNow)
            throw new InvalidOperationException("Session expired. Please verify your email again.");

        // 이미 회원가입이 완료된 경우 (Password가 비어있지 않으면)
        if (!string.IsNullOrEmpty(user.Password))
            throw new InvalidOperationException("Registration already completed");

        // 닉네임 중복 체크
        if (await _userService.GetUserByNicknameAsync(request.Nickname) != null)
            throw new InvalidOperationException("Nickname already exists");

        // 핸드폰 번호 중복 체크 (입력한 경우만)
        if (!string.IsNullOrEmpty(request.Phone))
        {
            if (await _userService.GetUserByPhoneAsync(request.Phone) != null)
                throw new InvalidOperationException("Phone number already exists");
        }

        // 비밀번호 해싱
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // User 정보 업데이트 + 세션 토큰 삭제
        user.Password = hashedPassword;
        user.Nickname = request.Nickname;
        user.Phone = request.Phone;
        user.RegistrationSessionToken = null;
        user.RegistrationSessionExpiry = null;
        user.UpdatedAt = DateTime.UtcNow;

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

        _logger.LogInformation("User registration completed: {Email}", user.Email);

        // 회원가입 성공 응답 (자동 로그인 없음)
        return new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            Nickname = user.Nickname,
            Verified = user.Verified
        };
    }

    // 6자리 인증 코드 생성
    private string GenerateVerificationCode()
    {
        var random = new Random();
        return random.Next(100000, 999999).ToString();
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

        // 이메일이 없거나 비밀번호가 틀린 경우 동일한 에러 메시지
        if (user == null || !isPasswordValid)
            throw new InvalidOperationException("Email or Password is not correct");

        if (!user.Verified)
            throw new InvalidOperationException("Account is not verified");

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
}