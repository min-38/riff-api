using System.ComponentModel.DataAnnotations;
using api.DTOs.Requests;

namespace api.Tests.Validation;

public class VerifyCodeRequestValidationTests
{
    private List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }

    [Fact]
    public void VerifyCodeRequest_ShouldPass_WhenAllFieldsAreValid()
    {
        // Arrange
        var request = new VerifyCodeRequest
        {
            Email = "test@test.com",
            Code = "123456"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void VerifyCodeRequest_ShouldFail_WhenEmailIsEmpty()
    {
        // Arrange
        var request = new VerifyCodeRequest
        {
            Email = "",
            Code = "123456"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage == "Email is required");
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@example.com")]
    [InlineData("test@")]
    public void VerifyCodeRequest_ShouldFail_WhenEmailFormatIsInvalid(string invalidEmail)
    {
        // Arrange
        var request = new VerifyCodeRequest
        {
            Email = invalidEmail,
            Code = "123456"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage == "Invalid email format");
    }

    [Fact]
    public void VerifyCodeRequest_ShouldFail_WhenCodeIsEmpty()
    {
        // Arrange
        var request = new VerifyCodeRequest
        {
            Email = "test@test.com",
            Code = ""
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage == "Verification code is required");
    }

    [Theory]
    [InlineData("12345")]   // Too short
    [InlineData("1234567")] // Too long
    [InlineData("abcdef")]  // Not digits
    [InlineData("12345a")]  // Mixed
    public void VerifyCodeRequest_ShouldFail_WhenCodeIsInvalid(string invalidCode)
    {
        // Arrange
        var request = new VerifyCodeRequest
        {
            Email = "test@test.com",
            Code = invalidCode
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage!.Contains("6 digits"));
    }

    [Theory]
    [InlineData("000000")]
    [InlineData("123456")]
    [InlineData("999999")]
    public void VerifyCodeRequest_ShouldPass_WhenCodeIsValid(string validCode)
    {
        // Arrange
        var request = new VerifyCodeRequest
        {
            Email = "test@test.com",
            Code = validCode
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }
}
