using System.ComponentModel.DataAnnotations;
using api.DTOs.Requests;

namespace api.Tests.Validation;

public class CheckEmailRequestValidationTests
{
    private List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }

    // 유효한 이메일이면 성공
    [Fact]
    public void CheckEmailRequest_ShouldPass_WhenEmailIsValid()
    {
        // Arrange
        var request = new CheckEmailRequest
        {
            Email = "test@test.com"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    // 이메일이 비어있으면 실패
    [Fact]
    public void CheckEmailRequest_ShouldFail_WhenEmailIsEmpty()
    {
        // Arrange
        var request = new CheckEmailRequest
        {
            Email = ""
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage == "Email is required");
    }

    // 잘못된 이메일 형식이면 실패
    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@example.com")]
    [InlineData("test@")]
    [InlineData("test")]
    public void CheckEmailRequest_ShouldFail_WhenEmailFormatIsInvalid(string invalidEmail)
    {
        // Arrange
        var request = new CheckEmailRequest
        {
            Email = invalidEmail
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage == "Invalid email format");
    }
}
