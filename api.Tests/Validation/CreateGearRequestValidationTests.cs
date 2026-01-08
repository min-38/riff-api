using FluentValidation.TestHelper;
using api.DTOs.Requests;
using api.Models.Enums;
using api.Validation;

namespace api.Tests.Validation;

public class CreateGearRequestValidationTests
{
    private readonly CreateGearRequestValidation _validator;

    public CreateGearRequestValidationTests()
    {
        _validator = new CreateGearRequestValidation();
    }

    #region Title 검증

    [Fact]
    public void Title_Empty_ShouldHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "",
            Description = "Valid description for testing purposes",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Title_TooShort_ShouldHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "A",
            Description = "Valid description for testing purposes",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Title_TooLong_ShouldHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = new string('A', 101),
            Description = "Valid description for testing purposes",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Title_Valid_ShouldNotHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Fender Stratocaster",
            Description = "Valid description for testing purposes",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    #endregion

    #region MainImageIndex 검증

    [Fact]
    public void MainImageIndex_Negative_ShouldHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "Valid description for testing purposes",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul,
            MainImageIndex = -1
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("MainImageIndex.Value");
    }

    [Fact]
    public void MainImageIndex_Valid_ShouldNotHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "Valid description for testing purposes",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul,
            MainImageIndex = 0
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.MainImageIndex);
    }

    #endregion

    #region Description 검증

    [Fact]
    public void Description_Empty_ShouldHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_TooShort_ShouldHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "Short",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_TooLong_ShouldHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = new string('A', 1001),
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_Valid_ShouldNotHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "This is a valid description with enough length",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    #endregion

    #region Price 검증

    [Fact]
    public void Price_Negative_ShouldHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "Valid description for testing purposes",
            Price = -100,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Price);
    }

    [Fact]
    public void Price_TooHigh_ShouldNotHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "Valid description for testing purposes",
            Price = 100_000_001,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Price);
    }

    [Fact]
    public void Price_Valid_ShouldNotHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "Valid description for testing purposes",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Price);
    }

    [Fact]
    public void Price_Zero_ShouldNotHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "Valid description for testing purposes",
            Price = 0,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Price);
    }

    #endregion

    #region Category-SubCategory-Detail 매핑 검증

    [Fact]
    public void CategorySubCategory_InstrumentWithGuitar_ShouldNotHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "Valid description for testing purposes",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CategorySubCategory_AudioWithGuitar_ShouldHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "Valid description for testing purposes",
            Price = 100000,
            Category = GearCategory.Audio,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void SubCategoryDetail_GuitarWithElectricBass_ShouldHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "Valid description for testing purposes",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.ElectricBass,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void CategorySubCategory_BassWithElectricBass_ShouldNotHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "Valid description for testing purposes",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Bass,
            DetailCategory = GearDetailCategory.ElectricBass,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CategorySubCategory_DrumWithAcousticDrum_ShouldNotHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "Valid description for testing purposes",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Drum,
            DetailCategory = GearDetailCategory.AcousticDrum,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CategorySubCategory_KeyboardWithPiano_ShouldNotHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "Valid description for testing purposes",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Keyboard,
            DetailCategory = GearDetailCategory.Piano,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CategorySubCategory_EffectsWithPedal_ShouldNotHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "Valid description for testing purposes",
            Price = 100000,
            Category = GearCategory.Audio,
            SubCategory = GearSubCategory.Effects,
            DetailCategory = GearDetailCategory.Pedal,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CategorySubCategory_AudioInterfaceWithUsbInterface_ShouldNotHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "Valid description for testing purposes",
            Price = 100000,
            Category = GearCategory.Audio,
            SubCategory = GearSubCategory.AudioInterface,
            DetailCategory = GearDetailCategory.UsbInterface,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CategorySubCategory_MicrophoneWithCondenser_ShouldNotHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "Valid description for testing purposes",
            Price = 100000,
            Category = GearCategory.Audio,
            SubCategory = GearSubCategory.Microphone,
            DetailCategory = GearDetailCategory.Condenser,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CategorySubCategory_AccessoryWithCable_ShouldNotHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "Valid description for testing purposes",
            Price = 100000,
            Category = GearCategory.Accessory,
            SubCategory = GearSubCategory.Cable,
            DetailCategory = GearDetailCategory.InstrumentCable,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CategorySubCategory_EtcWithOther_ShouldNotHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "Valid description for testing purposes",
            Price = 100000,
            Category = GearCategory.Etc,
            SubCategory = GearSubCategory.Other,
            DetailCategory = GearDetailCategory.Other,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region Condition 검증

    [Fact]
    public void Condition_Valid_ShouldNotHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "Valid description for testing purposes",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Condition);
    }

    #endregion

    #region TradeMethod 검증

    [Fact]
    public void TradeMethod_Valid_ShouldNotHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "Valid description for testing purposes",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Both,
            Region = Region.Seoul
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.TradeMethod);
    }

    #endregion

    #region Region 검증

    [Fact]
    public void Region_Valid_ShouldNotHaveValidationError()
    {
        var request = new CreateGearRequest
        {
            Title = "Valid Title",
            Description = "Valid description for testing purposes",
            Price = 100000,
            Category = GearCategory.Instrument,
            SubCategory = GearSubCategory.Guitar,
            DetailCategory = GearDetailCategory.Electric,
            Condition = GearCondition.Good,
            TradeMethod = TradeMethod.Direct,
            Region = Region.Busan
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Region);
    }

    #endregion
}
