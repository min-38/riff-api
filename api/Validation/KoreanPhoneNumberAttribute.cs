using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace api.Validation;

/// <summary>
/// 한국 휴대폰 번호 검증: 010-1234-5678, 01012345678, 010 1234 5678 형식 지원
/// </summary>
public class KoreanPhoneNumberAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string phone)
        {
            return new ValidationResult("Phone number is required");
        }

        // 공백과 하이픈 제거
        var cleanedPhone = Regex.Replace(phone, @"[\s-]", "");

        // 한국 휴대폰 번호 형식: 010으로 시작, 총 11자리
        // 010, 011, 016, 017, 018, 019 지원
        if (!Regex.IsMatch(cleanedPhone, @"^01[0-9]\d{7,8}$"))
        {
            return new ValidationResult("Invalid Korean phone number format. Expected format: 010-1234-5678 or 01012345678");
        }

        return ValidationResult.Success;
    }
}
