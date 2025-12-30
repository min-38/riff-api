using System.ComponentModel.DataAnnotations;
using api.Validation;

namespace api.DTOs.Requests;

public class RegisterRequest
{
    [Required(ErrorMessage = "Email is required")]
    [ValidEmail]
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

    // 전화번호는 선택 사항
    [KoreanPhoneNumber]
    public string? Phone { get; set; }

    // 이용약관 동의 (필수)
    [Required(ErrorMessage = "Terms of service agreement is required")]
    public bool TermsOfServiceAgreed { get; set; }

    // 개인정보처리방침 동의 (필수)
    [Required(ErrorMessage = "Privacy policy agreement is required")]
    public bool PrivacyPolicyAgreed { get; set; }

    // 마케팅 수신 동의 (선택)
    public bool MarketingAgreed { get; set; } = false;
}
