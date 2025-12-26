using System.ComponentModel.DataAnnotations;

namespace api.DTOs.Requests;

public class ResendVerificationRequest
{
    [Required(ErrorMessage = "Verification token is required")]
    public string VerificationToken { get; set; } = null!;

    // 3번 시도하면 CaptchaToken 전달 필요
    public string? CaptchaToken { get; set; }
}
