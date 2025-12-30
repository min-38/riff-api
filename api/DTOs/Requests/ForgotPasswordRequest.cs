using System.ComponentModel.DataAnnotations;
using api.Validation;

namespace api.DTOs.Requests;

public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Email is required")]
    [ValidEmail]
    public string Email { get; set; } = null!;

    public string? CaptchaToken { get; set; }
}
