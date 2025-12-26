using System.ComponentModel.DataAnnotations;
using api.DTOs.Requests;

namespace api.Tests.Validation;

public class RegisterRequestValidationTests
{
    private List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }

    [Fact]
    public void RegisterRequest_ShouldPass_WhenAllFieldsAreValid()
    {
        // Given
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "test",
            Phone = "010-1234-5678",
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true,
            MarketingAgreed = false
        };

        // When
        var results = ValidateModel(request);

        // Then
        Assert.Empty(results);
    }

    #region 이메일 유효성 검사

    [Fact]
    public void RegisterRequest_ShouldFail_WhenEmailIsEmpty()
    {
        // Given
        var request = new RegisterRequest
        {
            Email = "",
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "test",
            Phone = "010-1234-5678",
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true,
            MarketingAgreed = false
        };

        // When
        var results = ValidateModel(request);

        // Then
        Assert.Contains(results, r => r.ErrorMessage == "Email is required");
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@example.com")]
    [InlineData("test@")]
    [InlineData("test")]
    public void RegisterRequest_ShouldFail_WhenEmailFormatIsInvalid(string invalidEmail)
    {
        // Given
        var request = new RegisterRequest
        {
            Email = invalidEmail,
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "testuser",
            Phone = "010-1234-5678",
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true,
            MarketingAgreed = false
        };

        // When
        var results = ValidateModel(request);

        // Then
        Assert.Contains(results, r => r.ErrorMessage == "Invalid email format");
    }

    #endregion


    #region Password Validation Tests

    [Fact]
    public void RegisterRequest_ShouldFail_WhenPasswordIsTooShort()
    {
        // Given
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Pass1!",  // 6자
            PasswordConfirm = "Pass1!",
            Nickname = "testuser",
            Phone = "010-1234-5678",
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true,
            MarketingAgreed = false
        };

        // When
        var results = ValidateModel(request);

        // Then
        Assert.Contains(results, r => r.ErrorMessage!.Contains("must be between 8 and 32 characters"));
    }

    [Fact]
    public void RegisterRequest_ShouldFail_WhenPasswordIsTooLong()
    {
        // Arrange
        var longPassword = "Password123!" + new string('a', 25);  // 38자
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = longPassword,
            PasswordConfirm = longPassword,
            Nickname = "testuser",
            Phone = "010-1234-5678",
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true,
            MarketingAgreed = false
        };

        // When
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage!.Contains("must be between 8 and 32 characters"));
    }

    [Fact]
    public void RegisterRequest_ShouldFail_WhenPasswordHasNoUppercase()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "password123!",  // 대문자 없음
            PasswordConfirm = "password123!",
            Nickname = "testuser",
            Phone = "010-1234-5678",
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true,
            MarketingAgreed = false
        };

        // When
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage!.Contains("uppercase letter"));
    }

    [Fact]
    public void RegisterRequest_ShouldFail_WhenPasswordHasNoLowercase()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "PASSWORD123!",  // 소문자 없음
            PasswordConfirm = "PASSWORD123!",
            Nickname = "testuser",
            Phone = "010-1234-5678",
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true,
            MarketingAgreed = false
        };

        // When
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage!.Contains("lowercase letter"));
    }

    [Fact]
    public void RegisterRequest_ShouldFail_WhenPasswordHasNoNumber()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Password!",  // 숫자 없음
            PasswordConfirm = "Password!",
            Nickname = "testuser",
            Phone = "010-1234-5678",
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true,
            MarketingAgreed = false
        };

        // When
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage!.Contains("number"));
    }

    [Fact]
    public void RegisterRequest_ShouldFail_WhenPasswordHasNoSpecialCharacter()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Password123",  // 특수문자 없음
            PasswordConfirm = "Password123",
            Nickname = "testuser",
            Phone = "010-1234-5678",
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true,
            MarketingAgreed = false
        };

        // When
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage!.Contains("special character"));
    }

    #endregion

    #region PasswordConfirm Validation Tests

    [Fact]
    public void RegisterRequest_ShouldFail_WhenPasswordConfirmDoesNotMatch()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Password123!",
            PasswordConfirm = "DifferentPassword123!",  // 불일치
            Nickname = "testuser",
            Phone = "010-1234-5678",
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true,
            MarketingAgreed = false
        };

        // When
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage!.Contains("do not match"));
    }

    [Fact]
    public void RegisterRequest_ShouldFail_WhenPasswordConfirmIsEmpty()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Password123!",
            PasswordConfirm = "",
            Nickname = "testuser",
            Phone = "010-1234-5678",
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true,
            MarketingAgreed = false
        };

        // When
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage == "Password confirmation is required");
    }

    [Fact]
    public void RegisterRequest_ShouldPass_WhenPasswordAndPasswordConfirmMatch()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Password123!",
            PasswordConfirm = "Password123!",  // 일치
            Nickname = "testuser",
            Phone = "010-1234-5678",
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true,
            MarketingAgreed = false
        };

        // When
        var results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    #endregion

    #region Nickname Validation Tests

    [Fact]
    public void RegisterRequest_ShouldFail_WhenNicknameIsTooShort()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "a",  // 1자
            Phone = "010-1234-5678",
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true,
            MarketingAgreed = false
        };

        // When
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage!.Contains("between 2 and 15 characters"));
    }

    [Fact]
    public void RegisterRequest_ShouldFail_WhenNicknameIsTooLong()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "thisistoolongnickname",  // 21자
            Phone = "010-1234-5678",
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true,
            MarketingAgreed = false
        };

        // When
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage!.Contains("between 2 and 15 characters"));
    }

    [Theory]
    [InlineData("ab")]                // 2자 (최소)
    [InlineData("testuser")]          // 8자
    [InlineData("testusernamefif")]   // 15자 (최대)
    public void RegisterRequest_ShouldPass_WhenNicknameIsValidLength(string nickname)
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = nickname,
            Phone = "010-1234-5678",
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true,
            MarketingAgreed = false
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    #endregion

    #region Phone Validation Tests

    [Theory]
    [InlineData("010-1234-5678")]
    [InlineData("01012345678")]
    [InlineData("010 1234 5678")]
    [InlineData("011-1234-5678")]
    [InlineData("016-123-4567")]
    [InlineData("017-1234-5678")]
    [InlineData("018-123-4567")]
    [InlineData("019-1234-5678")]
    public void RegisterRequest_ShouldPass_WhenPhoneNumberIsValid(string phone)
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "testuser",
            Phone = phone,
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true,
            MarketingAgreed = false
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    [Theory]
    [InlineData("02-1234-5678")]    // 서울 지역번호
    [InlineData("031-123-4567")]    // 경기 지역번호
    [InlineData("1234567890")]      // 010으로 시작 안함
    [InlineData("010-12-5678")]     // 자리수 부족
    [InlineData("010-1234-56789")]  // 자리수 초과
    public void RegisterRequest_ShouldFail_WhenPhoneNumberIsInvalid(string phone)
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@test.com",
            Password = "Password123!",
            PasswordConfirm = "Password123!",
            Nickname = "testuser",
            Phone = phone,
            TermsOfServiceAgreed = true,
            PrivacyPolicyAgreed = true,
            MarketingAgreed = false
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.ErrorMessage!.Contains("Invalid Korean phone number format"));
    }

    #endregion
}
