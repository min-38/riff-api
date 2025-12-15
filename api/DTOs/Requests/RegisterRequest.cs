using System.ComponentModel.DataAnnotations;
using api.Validation;

namespace api.DTOs.Requests;

public class RegisterRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Registration session token is required")]
    public string SessionToken { get; set; } = null!;

    [Required(ErrorMessage = "Password is required")]
    [StrongPassword]
    public string Password { get; set; } = null!;

    [Required(ErrorMessage = "Password confirmation is required")]
    [PasswordConfirmation("Password")]
    public string PasswordConfirm { get; set; } = null!;

    [Required(ErrorMessage = "Nickname is required")]
    [StringLength(15, MinimumLength = 2, ErrorMessage = "Nickname must be between 2 and 15 characters")]
    public string Nickname { get; set; } = null!;

    // 전화번호는 선택 사항
    [KoreanPhoneNumber]
    public string? Phone { get; set; }
}
