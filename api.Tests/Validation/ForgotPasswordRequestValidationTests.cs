// 패스워드 재설정 요청 DTO에 대한 유효성 검사 테스트

using System.ComponentModel.DataAnnotations;
using api.DTOs.Requests;

namespace api.Tests.Validation;

public class ForgotPasswordRequestValidationTests
{
    private List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }

    #region 성공 케이스

    [Fact]
    public void ForgotPasswordRequest_ShouldPass_WhenEmailIsValid()
    {
        // Arrange
        var request = new ForgotPasswordRequest
        {
            Email = "test@test.com"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void ForgotPasswordRequest_ShouldPass_WhenEmailIsValidAndCaptchaTokenProvided()
    {
        // Arrange
        var request = new ForgotPasswordRequest
        {
            Email = "test@test.com",
            CaptchaToken = "valid_captcha_token"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void ForgotPasswordRequest_ShouldPass_WhenCaptchaTokenIsNull()
    {
        // Arrange
        var request = new ForgotPasswordRequest
        {
            Email = "test@test.com",
            CaptchaToken = null // 선택적 필드
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    #endregion

    #region 이메일 유효성 검사

    [Fact]
    public void ForgotPasswordRequest_ShouldFail_WhenEmailIsNull()
    {
        // Arrange
        var request = new ForgotPasswordRequest
        {
            Email = null!
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.ErrorMessage == "Email is required");
    }

    [Fact]
    public void ForgotPasswordRequest_ShouldFail_WhenEmailIsEmpty()
    {
        // Arrange
        var request = new ForgotPasswordRequest
        {
            Email = ""
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.ErrorMessage == "Email is required");
    }

    [Fact]
    public void ForgotPasswordRequest_ShouldFail_WhenEmailIsWhitespace()
    {
        // Arrange
        var request = new ForgotPasswordRequest
        {
            Email = "   "
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.ErrorMessage == "Email is required");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("invalid@")]
    [InlineData("@invalid.com")]
    [InlineData("invalid@com")]
    [InlineData("invalid email@example.com")]
    public void ForgotPasswordRequest_ShouldFail_WhenEmailFormatIsInvalid(string invalidEmail)
    {
        // Arrange
        var request = new ForgotPasswordRequest
        {
            Email = invalidEmail
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.ErrorMessage == "Invalid email format");
    }

    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name@example.com")]
    [InlineData("user+tag@example.co.kr")]
    [InlineData("test123@test-domain.com")]
    public void ForgotPasswordRequest_ShouldPass_WhenEmailFormatIsValid(string validEmail)
    {
        // Arrange
        var request = new ForgotPasswordRequest
        {
            Email = validEmail
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    #endregion

    #region CaptchaToken 검증

    [Fact]
    public void ForgotPasswordRequest_ShouldPass_WhenCaptchaTokenIsEmpty()
    {
        // Arrange
        var request = new ForgotPasswordRequest
        {
            Email = "test@example.com",
            CaptchaToken = ""
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    #endregion
}
