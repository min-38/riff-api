using System.ComponentModel.DataAnnotations;
using api.DTOs.Requests;

namespace api.Tests.Validation;

public class ResendVerificationRequestValidationTests
{
    private List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }

    // 유효한 토큰일 때 통과
    [Fact]
    public void ResendVerificationRequest_ShouldPass_WhenVerificationTokenIsValid()
    {
        // Arrange
        var request = new ResendVerificationRequest
        {
            VerificationToken = "valid_token_12345"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    // 토큰이 비어있을 때 실패
    [Fact]
    public void ResendVerificationRequest_ShouldFail_WhenVerificationTokenIsEmpty()
    {
        // Arrange
        var request = new ResendVerificationRequest
        {
            VerificationToken = ""
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage == "Verification token is required");
    }
}
