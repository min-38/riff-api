using System.ComponentModel.DataAnnotations;
using api.DTOs.Requests;

namespace api.Tests.Validation;

public class RefreshTokenRequestValidationTests
{
    private List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }

    [Fact]
    public void RefreshTokenRequest_ShouldPass_WhenRefreshTokenIsValid()
    {
        // Given
        var request = new RefreshTokenRequest
        {
            RefreshToken = "valid_refresh_token_string"
        };

        // When
        var results = ValidateModel(request);

        // Then
        Assert.Empty(results);
    }

    [Fact]
    public void RefreshTokenRequest_ShouldFail_WhenRefreshTokenIsEmpty()
    {
        // Given
        var request = new RefreshTokenRequest
        {
            RefreshToken = ""
        };

        // When
        var results = ValidateModel(request);

        // Then
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("RefreshToken") || r.ErrorMessage!.Contains("required"));
    }

    [Fact]
    public void RefreshTokenRequest_ShouldFail_WhenRefreshTokenIsNull()
    {
        // Given
        var request = new RefreshTokenRequest
        {
            RefreshToken = null!
        };

        // When
        var results = ValidateModel(request);

        // Then
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("RefreshToken") || r.ErrorMessage!.Contains("required"));
    }

    [Fact]
    public void RefreshTokenRequest_ShouldFail_WhenRefreshTokenIsWhiteSpace()
    {
        // Given
        var request = new RefreshTokenRequest
        {
            RefreshToken = "   "
        };

        // When
        var results = ValidateModel(request);

        // Then
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("RefreshToken") || r.ErrorMessage!.Contains("required"));
    }
}
