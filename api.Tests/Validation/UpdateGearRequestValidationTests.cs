using FluentValidation.TestHelper;
using api.DTOs.Requests;
using api.Models.Enums;
using api.Validation;

namespace api.Tests.Validation;

public class UpdateGearRequestValidationTests
{
    private readonly UpdateGearRequestValidation _validator;

    public UpdateGearRequestValidationTests()
    {
        _validator = new UpdateGearRequestValidation();
    }

    #region Title 검증

    [Fact]
    public void Title_Empty_ShouldHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            Title = ""
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Title_TooShort_ShouldHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            Title = "A"
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Title_TooLong_ShouldHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            Title = new string('A', 101)
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Title_Valid_ShouldNotHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            Title = "Updated Title"
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Title_Null_ShouldNotHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            Title = null
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    #endregion

    #region Description 검증

    [Fact]
    public void Description_TooShort_ShouldHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            Description = "Short"
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_TooLong_ShouldHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            Description = new string('A', 1001)
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_Valid_ShouldNotHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            Description = "Valid description text"
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_Null_ShouldNotHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            Description = null
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    #endregion

    #region Price 검증

    [Fact]
    public void Price_Negative_ShouldHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            Price = -100
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Price!.Value);
    }

    [Fact]
    public void Price_TooHigh_ShouldHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            Price = 100_000_001
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Price!.Value);
    }

    [Fact]
    public void Price_Valid_ShouldNotHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            Price = 150000
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Price);
    }

    [Fact]
    public void Price_Null_ShouldNotHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            Price = null
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Price);
    }

    #endregion

    #region Category-SubCategory-Detail 매핑 검증

    [Fact]
    public void CategorySubCategory_InstrumentWithGuitar_ShouldNotHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CategorySubCategory_AudioWithGuitar_ShouldHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            Category = GearCategory.Audio,
            SubCategory = GearSubCategory.Guitar
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void SubCategoryDetail_GuitarWithElectricBass_ShouldHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.ElectricBass
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void CategorySubCategory_OnlyCategoryProvided_ShouldNotHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            Category = GearCategory.Instrument,
            SubCategory = null
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CategorySubCategory_OnlySubCategoryProvided_ShouldNotHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            Category = null,
            SubCategory = GearSubCategory.Guitar
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void SubCategoryDetail_OnlyDetailProvided_ShouldNotHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            DetailCategory = GearDetailCategory.Electric
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region Status 검증

    [Fact]
    public void Status_Valid_ShouldNotHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            Status = GearStatus.Selling
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void Status_Null_ShouldNotHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            Status = null
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    #endregion

    #region KeepImageUrls 검증

    [Fact]
    public void KeepImageUrls_ValidUrls_ShouldNotHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            KeepImageUrls = ["gears/image-1.jpg", "gears/image-2.jpg"]
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.KeepImageUrls);
    }

    [Fact]
    public void KeepImageUrls_EmptyList_ShouldNotHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            KeepImageUrls = []
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.KeepImageUrls);
    }

    [Fact]
    public void KeepImageUrls_Null_ShouldNotHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            KeepImageUrls = null
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.KeepImageUrls);
    }

    [Fact]
    public void KeepImageUrls_ContainsEmptyString_ShouldHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            KeepImageUrls = ["gears/image-1.jpg", "", "gears/image-2.jpg"]
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.KeepImageUrls);
    }

    [Fact]
    public void KeepImageUrls_ContainsWhitespace_ShouldHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            KeepImageUrls = ["gears/image-1.jpg", "   ", "gears/image-2.jpg"]
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.KeepImageUrls);
    }

    #endregion

    #region MainImageIndex 검증

    [Fact]
    public void MainImageIndex_Negative_ShouldHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            MainImageIndex = -1
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("MainImageIndex.Value");
    }

    [Fact]
    public void MainImageIndex_Valid_ShouldNotHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            MainImageIndex = 0
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.MainImageIndex);
    }

    #endregion

    #region 전체 업데이트 시나리오

    [Fact]
    public void FullUpdate_AllFieldsValid_ShouldNotHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            Title = "Updated Fender Stratocaster",
            Description = "Updated description with all details",
            Price = 1400000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Both,
            Region = Region.Busan,
            Status = GearStatus.Selling,
            KeepImageUrls = ["gears/image-1.jpg"]
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PartialUpdate_OnlyTitle_ShouldNotHaveValidationError()
    {
        var request = new UpdateGearRequest
        {
            Title = "Only Title Updated"
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyUpdate_AllFieldsNull_ShouldNotHaveValidationError()
    {
        var request = new UpdateGearRequest();

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion
}
