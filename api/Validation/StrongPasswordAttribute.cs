using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace api.Validation;

/// <summary>
/// 강력한 패스워드 검증: 대소문자 + 숫자 + 특수문자 포함, 8-32자
/// </summary>
public class StrongPasswordAttribute : ValidationAttribute
{
    private const int MinLength = 8;
    private const int MaxLength = 32;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string password)
        {
            return new ValidationResult("Password is required");
        }

        if (password.Length < MinLength || password.Length > MaxLength)
        {
            return new ValidationResult($"Password must be between {MinLength} and {MaxLength} characters");
        }

        // 대문자 포함 확인
        if (!Regex.IsMatch(password, @"[A-Z]"))
        {
            return new ValidationResult("Password must contain at least one uppercase letter");
        }

        // 소문자 포함 확인
        if (!Regex.IsMatch(password, @"[a-z]"))
        {
            return new ValidationResult("Password must contain at least one lowercase letter");
        }

        // 숫자 포함 확인
        if (!Regex.IsMatch(password, @"[0-9]"))
        {
            return new ValidationResult("Password must contain at least one number");
        }

        // 특수문자 포함 확인
        if (!Regex.IsMatch(password, @"[!@#$%^&*(),.?""':{}|<>]"))
        {
            return new ValidationResult("Password must contain at least one special character (!@#$%^&*(),.?\"':{}|<>)");
        }

        return ValidationResult.Success;
    }
}
