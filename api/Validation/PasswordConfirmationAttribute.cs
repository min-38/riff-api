using System.ComponentModel.DataAnnotations;

namespace api.Validation;

/// <summary>
/// 패스워드와 패스워드 확인이 일치하는지 검증
/// </summary>
public class PasswordConfirmationAttribute : ValidationAttribute
{
    private readonly string _passwordPropertyName;

    public PasswordConfirmationAttribute(string passwordPropertyName = "Password")
    {
        _passwordPropertyName = passwordPropertyName;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var passwordProperty = validationContext.ObjectType.GetProperty(_passwordPropertyName);
        if (passwordProperty == null)
        {
            return new ValidationResult($"Unknown property: {_passwordPropertyName}");
        }

        var passwordValue = passwordProperty.GetValue(validationContext.ObjectInstance) as string;
        var confirmPasswordValue = value as string;

        if (passwordValue != confirmPasswordValue)
        {
            return new ValidationResult("Password and password confirmation do not match");
        }

        return ValidationResult.Success;
    }
}
