using System.ComponentModel.DataAnnotations;
using api.DTOs.Requests;

namespace api.Tests.Validation;

public class LoginRequestValidationTests
{
    private List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }

    // 모든 필드 유효
    [Fact]
    public void LoginRequest_ShouldPass_WhenAllFieldsAreValid()
    {
        // Given
        var request = new LoginRequest
        {
            Email = "test@test.com",
            Password = "Password123!",
        };

        // When
        var results = ValidateModel(request);

        // Then
        Assert.Empty(results);
    }

    #region Email Validation Tests

    [Fact]
    public void LoginRequest_ShouldFail_WhenEmailIsEmpty()
    {
        // Given
        var request = new LoginRequest
        {
            Email = "",
            Password = "Password123!"
        };

        // When
        var results = ValidateModel(request);

        // Then
        Assert.Contains(results, r => r.ErrorMessage == "Email is required");
    }

    #endregion

    #region Password Validation Tests

    [Fact]
    public void LoginRequest_ShouldFail_WhenPasswordIsEmpty()
    {
        // Given
        var request = new LoginRequest
        {
            Email = "test@test.com",
            Password = ""
        };

        // When
        var results = ValidateModel(request);

        // Then
        Assert.Contains(results, r => r.ErrorMessage!.Contains("Password is required"));
    }

    #endregion
}
