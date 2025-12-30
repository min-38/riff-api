using System.ComponentModel.DataAnnotations;
using api.Validation;

namespace api.DTOs.Requests;

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "Reset token is required")]
    public string ResetToken { get; set; } = null!;

    [Required(ErrorMessage = "New password is required")]
    [StrongPassword]
    public string NewPassword { get; set; } = null!;
}
