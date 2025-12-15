using System.ComponentModel.DataAnnotations;
using api.DTOs.Requests;

namespace api.Tests.Validation;

public class CheckNicknameRequestValidationTests
{
    private List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, validationContext, validationResults, true);

        return validationResults;
    }

    [Fact]
    public void CheckNicknameRequest_ShouldPass_WhenNicknameIsValid()
    {
        // Arrange
        var request = new CheckNicknameRequest
        {
            Nickname = "test"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void CheckNicknameRequest_ShouldFail_WhenNicknameIsEmpty()
    {
        // Arrange
        var request = new CheckNicknameRequest
        {
            Nickname = ""
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage == "Nickname is required");
    }

    [Fact]
    public void CheckNicknameRequest_ShouldFail_WhenNicknameIsTooShort()
    {
        // Arrange
        var request = new CheckNicknameRequest
        {
            Nickname = "a"  // 1자
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage!.Contains("between 2 and 15 characters"));
    }

    [Fact]
    public void CheckNicknameRequest_ShouldFail_WhenNicknameIsTooLong()
    {
        // Arrange
        var request = new CheckNicknameRequest
        {
            Nickname = "testtesttesttesttest"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage!.Contains("between 2 and 15 characters"));
    }

    [Theory]
    [InlineData("ab")]                // 2자 (최소)
    [InlineData("testtest")]          // 8자
    [InlineData("testtesttesttes")]   // 15자 (최대)
    public void CheckNicknameRequest_ShouldPass_WhenNicknameIsValidLength(string nickname)
    {
        // Arrange
        var request = new CheckNicknameRequest
        {
            Nickname = nickname
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }
}
