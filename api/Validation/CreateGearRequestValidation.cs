using FluentValidation;
using api.Constants;
using api.DTOs.Requests;
using api.Models.Enums;

namespace api.Validation;

public class CreateGearRequestValidation : AbstractValidator<CreateGearRequest>
{
    public CreateGearRequestValidation()
    {
        // Title 검증
        RuleFor(x => x.Title)
            .NotEmpty()
                .WithErrorCode(ErrorCodes.FIELD_REQUIRED)
            .MinimumLength(2)
                .WithErrorCode(ErrorCodes.FIELD_TOO_SHORT)
            .MaximumLength(100)
                .WithErrorCode(ErrorCodes.FIELD_TOO_LONG);

        // Description 검증
        RuleFor(x => x.Description)
            .NotEmpty()
                .WithErrorCode(ErrorCodes.FIELD_REQUIRED)
            .MinimumLength(15)
                .WithErrorCode(ErrorCodes.FIELD_TOO_SHORT)
            .MaximumLength(1000)
                .WithErrorCode(ErrorCodes.FIELD_TOO_LONG);

        // Price 검증
        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0)
                .WithErrorCode(ErrorCodes.PRICE_TOO_LOW);

        // Category 검증
        RuleFor(x => x.Category)
            .IsInEnum()
                .WithErrorCode(ErrorCodes.INVALID_CATEGORY);

        // SubCategory 검증
        RuleFor(x => x.SubCategory)
            .IsInEnum()
                .WithErrorCode(ErrorCodes.INVALID_SUBCATEGORY);

        // DetailCategory 검증
        RuleFor(x => x.DetailCategory)
            .IsInEnum()
                .WithErrorCode(ErrorCodes.INVALID_SUBCATEGORY);

        // Condition 검증
        RuleFor(x => x.Condition)
            .NotNull()
                .WithErrorCode(ErrorCodes.FIELD_REQUIRED)
            .IsInEnum()
                .WithErrorCode(ErrorCodes.INVALID_CONDITION);

        // TradeMethod 검증
        RuleFor(x => x.TradeMethod)
            .IsInEnum()
                .WithErrorCode(ErrorCodes.INVALID_TRADE_METHOD);

        // Region 검증
        RuleFor(x => x.Region)
            .IsInEnum()
                .WithErrorCode(ErrorCodes.INVALID_REGION);

        // 메인 이미지 검증
        When(x => x.MainImageIndex.HasValue, () =>
        {
            RuleFor(x => x.MainImageIndex!.Value)
                .GreaterThanOrEqualTo(0)
                    .WithErrorCode(ErrorCodes.INVALID_OPERATION);
        });

        // 대분류 -> 중분류 매핑 검증
        RuleFor(x => x)
            .Must(x => IsValidCategorySubCategoryMapping(x.Category, x.SubCategory))
                .WithErrorCode(ErrorCodes.CATEGORY_SUBCATEGORY_MISMATCH)
            .WithName("CategorySubCategory");

        // 중분류 -> 소분류 매핑 검증
        RuleFor(x => x)
            .Must(x => IsValidSubCategoryDetailCategoryMapping(x.SubCategory, x.DetailCategory))
                .WithErrorCode(ErrorCodes.CATEGORY_SUBCATEGORY_MISMATCH)
            .WithName("SubCategoryDetailCategory");
    }

    // 대분류 -> 중분류
    private static bool IsValidCategorySubCategoryMapping(GearCategory category, GearSubCategory subCategory)
    {
        return category switch
        {
            GearCategory.Instrument => subCategory
                is GearSubCategory.Guitar
                or GearSubCategory.Bass
                or GearSubCategory.Drum
                or GearSubCategory.Keyboard
                or GearSubCategory.Wind
                or GearSubCategory.StringInstrument,

            GearCategory.Audio => subCategory
                is GearSubCategory.Effects
                or GearSubCategory.Mixer
                or GearSubCategory.Amp
                or GearSubCategory.Speaker
                or GearSubCategory.Monitor
                or GearSubCategory.AudioInterface
                or GearSubCategory.Microphone
                or GearSubCategory.Headphone
                or GearSubCategory.Iem
                or GearSubCategory.Earphone,

            GearCategory.Accessory => subCategory is GearSubCategory.Cable
                or GearSubCategory.Stand
                or GearSubCategory.Case
                or GearSubCategory.Pick
                or GearSubCategory.StringAccessory
                or GearSubCategory.Drumstick,

            GearCategory.Etc => subCategory is GearSubCategory.Other,

            _ => false
        };
    }

    // 중분류 -> 소분류
    private static bool IsValidSubCategoryDetailCategoryMapping(GearSubCategory subCategory, GearDetailCategory detailCategory)
    {
        return subCategory switch
        {
            GearSubCategory.Guitar => detailCategory
                is GearDetailCategory.Electric
                or GearDetailCategory.Acoustic
                or GearDetailCategory.Classical,

            GearSubCategory.Bass => detailCategory
                is GearDetailCategory.ElectricBass
                or GearDetailCategory.AcousticBass
                or GearDetailCategory.UprightBass,

            GearSubCategory.Drum => detailCategory
                is GearDetailCategory.AcousticDrum
                or GearDetailCategory.ElectronicDrum
                or GearDetailCategory.Percussion,

            GearSubCategory.Keyboard => detailCategory
                is GearDetailCategory.Piano
                or GearDetailCategory.Synthesizer
                or GearDetailCategory.Midi
                or GearDetailCategory.Organ,

            GearSubCategory.Wind => detailCategory
                is GearDetailCategory.Saxophone
                or GearDetailCategory.Trumpet
                or GearDetailCategory.Flute
                or GearDetailCategory.Clarinet,

            GearSubCategory.StringInstrument => detailCategory
                is GearDetailCategory.Violin
                or GearDetailCategory.Viola
                or GearDetailCategory.Cello,

            GearSubCategory.Effects => detailCategory
                is GearDetailCategory.MultiEffects
                or GearDetailCategory.Pedal
                or GearDetailCategory.BassEffects
                or GearDetailCategory.AcousticEffects
                or GearDetailCategory.Pedalboard
                or GearDetailCategory.PowerSupply
                or GearDetailCategory.EffectsOther,

            GearSubCategory.Mixer => detailCategory is GearDetailCategory.Mixer,

            GearSubCategory.Amp => detailCategory
                is GearDetailCategory.Amp
                or GearDetailCategory.Preamp
                or GearDetailCategory.PowerAmp,

            GearSubCategory.Speaker => detailCategory
                is GearDetailCategory.PaSpeaker
                or GearDetailCategory.Subwoofer
                or GearDetailCategory.SpeakerSystem,

            GearSubCategory.Monitor => detailCategory
                is GearDetailCategory.Monitor
                or GearDetailCategory.StudioMonitor,

            GearSubCategory.AudioInterface => detailCategory
                is GearDetailCategory.UsbInterface
                or GearDetailCategory.ThunderboltInterface
                or GearDetailCategory.PcieInterface,

            GearSubCategory.Microphone => detailCategory
                is GearDetailCategory.Condenser
                or GearDetailCategory.Dynamic
                or GearDetailCategory.Ribbon
                or GearDetailCategory.WirelessMic,

            GearSubCategory.Headphone => detailCategory
                is GearDetailCategory.Headphone
                or GearDetailCategory.Headset,

            GearSubCategory.Iem => detailCategory is GearDetailCategory.Iem,

            GearSubCategory.Earphone => detailCategory is GearDetailCategory.Earphone,

            GearSubCategory.Cable => detailCategory
                is GearDetailCategory.InstrumentCable
                or GearDetailCategory.MicCable
                or GearDetailCategory.SpeakerCable
                or GearDetailCategory.PatchCable,

            GearSubCategory.Stand => detailCategory is GearDetailCategory.Stand,

            GearSubCategory.Case => detailCategory is GearDetailCategory.Case,

            GearSubCategory.Pick => detailCategory is GearDetailCategory.Pick,

            GearSubCategory.StringAccessory => detailCategory is GearDetailCategory.@String,

            GearSubCategory.Drumstick => detailCategory is GearDetailCategory.Drumstick,

            GearSubCategory.Other => detailCategory is GearDetailCategory.Other,

            _ => false
        };
    }
}
