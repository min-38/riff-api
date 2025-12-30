using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace api.Validation;

// 유효한 이메일 주소 검증
// "test@com", "te st@test.com" 등 잘못된 형식 방지
public class ValidEmailAttribute : ValidationAttribute
{
    // RFC 5322 기반 간소화된 이메일 정규식
    private const string EmailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // null 또는 빈 값은 [required] 속성에서 처리
        if (value is not string email || string.IsNullOrWhiteSpace(email))
            return ValidationResult.Success;

        // 이메일 형식 검증
        if (!Regex.IsMatch(email, EmailPattern))
            return new ValidationResult("Invalid email format");
        return ValidationResult.Success;
    }
}
