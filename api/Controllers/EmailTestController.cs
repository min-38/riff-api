using Microsoft.AspNetCore.Mvc;
using api.Services;
using api.Filters;

namespace api.Controllers;

[ApiController]
[Route("[controller]")]
[DevelopmentOnly]
public class EmailTestController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailTestController> _logger;

    public EmailTestController(IEmailService emailService, ILogger<EmailTestController> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    // 이메일 서버 연결 테스트
    [HttpGet("connection")]
    public async Task<IActionResult> TestConnection()
    {
        var (success, message) = await _emailService.TestSmtpConnectionAsync();

        if (success)
            return Ok(new { success = true, message });

        return BadRequest(new { success = false, message });
    }

    // 테스트 이메일 발송 - 그냥 메일이 보내짐
    [HttpPost("send")]
    public async Task<IActionResult> SendTestEmail()
    {
        var testEmail = Environment.GetEnvironmentVariable("TEST_EMAIL");

        if (string.IsNullOrEmpty(testEmail))
        {
            return BadRequest(new
            {
                success = false,
                message = "TEST_EMAIL environment variable is not set"
            });
        }

        var (success, message) = await _emailService.SendTestEmailAsync(testEmail);

        if (success)
        {
            return Ok(new
            {
                success = true,
                message,
                sentTo = testEmail
            });
        }

        return BadRequest(new
        {
            success = false,
            message,
            sentTo = testEmail
        });
    }

    // 인증 코드 이메일 테스트 발송
    [HttpPost("verification")]
    public async Task<IActionResult> SendVerificationTestEmail()
    {
        var testEmail = Environment.GetEnvironmentVariable("TEST_EMAIL");

        if (string.IsNullOrEmpty(testEmail))
        {
            return BadRequest(new
            {
                success = false,
                message = "TEST_EMAIL environment variable is not set"
            });
        }

        try
        {
            // 랜덤 6자리 인증 코드 생성
            var random = new Random();
            var verificationCode = random.Next(100000, 999999).ToString();

            await _emailService.SendVerificationEmailAsync(testEmail, verificationCode);

            return Ok(new
            {
                success = true,
                message = $"Verification email sent to {testEmail}",
                sentTo = testEmail,
                verificationCode = verificationCode // 개발 환경에서만 반환
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification test email");
            return BadRequest(new
            {
                success = false,
                message = ex.Message,
                sentTo = testEmail
            });
        }
    }

    // 특정 이메일로 테스트 발송
    [HttpPost("send-to")]
    public async Task<IActionResult> SendTestEmailTo([FromBody] SendTestEmailRequest request)
    {
        if (string.IsNullOrEmpty(request.Email))
        {
            return BadRequest(new
            {
                success = false,
                message = "Email is required"
            });
        }

        var (success, message) = await _emailService.SendTestEmailAsync(request.Email);

        if (success)
        {
            return Ok(new
            {
                success = true,
                message,
                sentTo = request.Email
            });
        }

        return BadRequest(new
        {
            success = false,
            message,
            sentTo = request.Email
        });
    }

    // 특정 이메일로 인증 코드 테스트 발송
    [HttpPost("verification-to")]
    public async Task<IActionResult> SendVerificationTestEmailTo([FromBody] SendTestEmailRequest request)
    {
        if (string.IsNullOrEmpty(request.Email))
        {
            return BadRequest(new
            {
                success = false,
                message = "Email is required"
            });
        }

        try
        {
            // 랜덤 6자리 인증 코드 생성
            var random = new Random();
            var verificationCode = random.Next(100000, 999999).ToString();

            await _emailService.SendVerificationEmailAsync(request.Email, verificationCode);

            return Ok(new
            {
                success = true,
                message = $"Verification email sent to {request.Email}",
                sentTo = request.Email,
                verificationCode = verificationCode // 개발 환경에서만 반환
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification test email");
            return BadRequest(new
            {
                success = false,
                message = ex.Message,
                sentTo = request.Email
            });
        }
    }
}

public record SendTestEmailRequest(string Email);
