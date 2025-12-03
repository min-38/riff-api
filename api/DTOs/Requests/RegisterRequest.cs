using System.ComponentModel.DataAnnotations;
using api.Validation;

namespace api.DTOs.Requests;

public class RegisterRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Password is required")]
    [StrongPassword]
    public string Password { get; set; } = null!;

    [Required(ErrorMessage = "Password confirmation is required")]
    [PasswordConfirmation("Password")]
    public string PasswordConfirm { get; set; } = null!;

    [Required(ErrorMessage = "Nickname is required")]
    [StringLength(15, MinimumLength = 2, ErrorMessage = "Nickname must be between 2 and 15 characters")]
    public string Nickname { get; set; } = null!;

    [Required(ErrorMessage = "Phone number is required")]
    [KoreanPhoneNumber]
    public string Phone { get; set; } = null!;

    [Required(ErrorMessage = "Firebase id token is required")]
    public string FirebaseIdToken { get; set; } = null!;
}
