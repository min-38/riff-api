using System.ComponentModel.DataAnnotations;
using api.DTOs.Requests;

namespace api.Tests.Validation;

public class SendVerificationRequestValidationTests
{
    private List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }

    [Fact]
    public void SendVerificationRequest_ShouldPass_WhenEmailIsValid()
    {
        // Arrange
        var request = new SendVerificationRequest
        {
            Email = "test@test.com"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void SendVerificationRequest_ShouldFail_WhenEmailIsEmpty()
    {
        // Arrange
        var request = new SendVerificationRequest
        {
            Email = ""
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage == "Email is required");
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@test.com")]
    [InlineData("test@")]
    [InlineData("test")]
    public void SendVerificationRequest_ShouldFail_WhenEmailFormatIsInvalid(string invalidEmail)
    {
        // Arrange
        var request = new SendVerificationRequest
        {
            Email = invalidEmail
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage == "Invalid email format");
    }
}
