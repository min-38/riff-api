using Microsoft.AspNetCore.Mvc;
using api.DTOs.Requests;
using api.DTOs.Responses;
using api.Services;
using api.Exceptions;

namespace api.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, IUserService userService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _userService = userService;
        _logger = logger;
    }

    // 이메일 중복 체크
    [HttpPost("check-email")]
    public async Task<IActionResult> CheckEmail([FromBody] CheckEmailRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(new { message = "Validation failed", errors });
        }

        try
        {
            var isAvailable = await _userService.IsEmailAvailableAsync(request.Email);
            return Ok(new { available = isAvailable });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking email availability");
            return StatusCode(500, new { message = "An error occurred while checking email" });
        }
    }

    // 닉네임 중복 체크
    [HttpPost("check-nickname")]
    public async Task<IActionResult> CheckNickname([FromBody] CheckNicknameRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(new { message = "Validation failed", errors });
        }

        try
        {
            var isAvailable = await _userService.IsNicknameAvailableAsync(request.Nickname);
            return Ok(new { available = isAvailable });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking nickname availability");
            return StatusCode(500, new { message = "An error occurred while checking nickname" });
        }
    }

    // 이메일 인증 (URL 토큰 기반)
    [HttpGet("verify-email/{token}")]
    public async Task<IActionResult> VerifyEmailByToken(string token)
    {
        try
        {
            var response = await _authService.VerifyEmailByTokenAsync(token);

            var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:3000";
            // Fragment(#) 사용으로 보안 강화 (서버 로그에 토큰 노출 방지)
            var redirectUrl = $"{frontendUrl}/auth/verify-success#accessToken={Uri.EscapeDataString(response.Token!)}&refreshToken={Uri.EscapeDataString(response.RefreshToken!)}&expiresAt={response.ExpiresAt:O}";

            return Redirect(redirectUrl);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Email verification failed");
            var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:3000";
            var errorUrl = $"{frontendUrl}/auth/verify-error";
            return Redirect(errorUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during email verification");
            var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:3000";
            var errorUrl = $"{frontendUrl}/auth/verify-error";
            return Redirect(errorUrl);
        }
    }

    // 회원가입
    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(new { message = "Validation failed", errors });
        }

        try
        {
            var response = await _authService.RegisterAsync(request);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already exists") || ex.Message.Contains("already completed"))
                return Conflict(new { message = ex.Message });
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing registration");
            return StatusCode(500, new { message = "An error occurred during registration" });
        }
    }

    // 인증 정보 조회 (토큰 기반)
    [HttpPost("verification-info")]
    public async Task<IActionResult> GetVerificationInfo([FromBody] VerificationInfoRequest request)
    {
        // 유효성 검증 실패 시
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(new { message = "Validation failed", errors });
        }

        try
        {
            var response = await _authService.GetVerificationInfoAsync(request.VerificationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving verification info");
            return StatusCode(500, new { message = "An error occurred while retrieving verification info" });
        }
    }

    // 인증 이메일 재전송 (토큰 기반)
    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerificationEmail([FromBody] ResendVerificationRequest request)
    {
        // 유효성 검증 실패 시
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(new { message = "Validation failed", errors });
        }

        try
        {
            var response = await _authService.ResendVerificationEmailAsync(request.VerificationToken, request.CaptchaToken);
            return Ok(response);
        }
        catch (RateLimitException ex)
        {
            return StatusCode(429, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resending verification email");
            return StatusCode(500, new { message = "An error occurred while resending verification email" });
        }
    }

    // 로그인
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        // 유효성 검사
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(new { message = "Validation failed", errors });
        }

        try
        {
            var response = await _authService.LogInAsync(request);
            return Ok(response);
        }
        catch (UnverifiedAccountException ex)
        {
            return StatusCode(403, new
            {
                message = ex.Message,
                verified = false,
                verificationToken = ex.VerificationToken,
                remainingCooldown = ex.RemainingCooldown
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user login");
            return StatusCode(500, new { message = "An error occurred during login" });
        }
    }

    // 로그아웃
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest? request = null)
    {
        try
        {
            var refreshToken = request?.RefreshToken;
            await _authService.LogOutAsync(refreshToken);
            return Ok(new { message = "Logout successful" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, new { message = "An error occurred during logout" });
        }
    }

    // Refresh access token
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var response = await _authService.RefreshAccessTokenAsync(request.RefreshToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(500, new { message = "An error occurred during token refresh" });
        }
    }
}
