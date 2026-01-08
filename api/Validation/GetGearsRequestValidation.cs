using FluentValidation;
using api.Constants;
using api.DTOs.Requests;
using api.Models.Enums;

namespace api.Validation;

public class GetGearsRequestValidation : AbstractValidator<GetGearsRequest>
{
    private static readonly string[] ValidSortByValues = ["created_at", "price", "view_count"];
    private static readonly string[] ValidSortOrderValues = ["asc", "desc"];

    public GetGearsRequestValidation()
    {
        // Page 검증
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
                .WithMessage("Page must be greater than or equal to 1")
                .WithErrorCode("INVALID_PAGE");

        // PageSize 검증
        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1)
                .WithMessage("PageSize must be greater than or equal to 1")
                .WithErrorCode("INVALID_PAGE_SIZE")
            .LessThanOrEqualTo(100)
                .WithMessage("PageSize must be less than or equal to 100")
                .WithErrorCode("INVALID_PAGE_SIZE");

        // SearchKeyword 검증
        When(x => !string.IsNullOrWhiteSpace(x.SearchKeyword), () =>
        {
            RuleFor(x => x.SearchKeyword)
                .MaximumLength(100)
                    .WithMessage("SearchKeyword must be less than or equal to 100 characters")
                    .WithErrorCode(ErrorCodes.FIELD_TOO_LONG);
        });

        // SortBy 검증
        RuleFor(x => x.SortBy)
            .NotEmpty()
                .WithErrorCode(ErrorCodes.FIELD_REQUIRED)
            .Must(sortBy => ValidSortByValues.Contains(sortBy.ToLower()))
                .WithMessage($"SortBy must be one of: {string.Join(", ", ValidSortByValues)}")
                .WithErrorCode("INVALID_SORT_BY");

        // SortOrder 검증
        RuleFor(x => x.SortOrder)
            .NotEmpty()
                .WithErrorCode(ErrorCodes.FIELD_REQUIRED)
            .Must(sortOrder => ValidSortOrderValues.Contains(sortOrder.ToLower()))
                .WithMessage($"SortOrder must be one of: {string.Join(", ", ValidSortOrderValues)}")
                .WithErrorCode("INVALID_SORT_ORDER");

        // 대분류 검증
        When(x => x.Category.HasValue, () =>
        {
            RuleFor(x => x.Category!.Value)
                .IsInEnum()
                    .WithErrorCode(ErrorCodes.INVALID_CATEGORY);
        });

        // 중분류 검증
        When(x => x.SubCategory.HasValue, () =>
        {
            RuleFor(x => x.SubCategory!.Value)
                .IsInEnum()
                    .WithErrorCode(ErrorCodes.INVALID_SUBCATEGORY);
        });

        // 소분류 검증
        When(x => x.DetailCategory.HasValue, () =>
        {
            RuleFor(x => x.DetailCategory!.Value)
                .IsInEnum()
                    .WithErrorCode(ErrorCodes.INVALID_SUBCATEGORY);
        });

        // Status 검증
        When(x => x.Status.HasValue, () =>
        {
            RuleFor(x => x.Status!.Value)
                .IsInEnum()
                    .WithErrorCode("INVALID_STATUS");
        });

        // Condition 검증
        When(x => x.Condition.HasValue, () =>
        {
            RuleFor(x => x.Condition!.Value)
                .IsInEnum()
                    .WithErrorCode(ErrorCodes.INVALID_CONDITION);
        });

        // TradeMethod 검증
        When(x => x.TradeMethod.HasValue, () =>
        {
            RuleFor(x => x.TradeMethod!.Value)
                .IsInEnum()
                    .WithErrorCode(ErrorCodes.INVALID_TRADE_METHOD);
        });

        // Region 검증
        When(x => x.Region.HasValue, () =>
        {
            RuleFor(x => x.Region!.Value)
                .IsInEnum()
                    .WithErrorCode(ErrorCodes.INVALID_REGION);
        });

        // 대분류 -> 중분류 매핑 검증
        When(x => x.Category.HasValue && x.SubCategory.HasValue, () =>
        {
            RuleFor(x => x)
                .Must(x => IsValidCategorySubCategoryMapping(x.Category!.Value, x.SubCategory!.Value))
                    .WithErrorCode(ErrorCodes.CATEGORY_SUBCATEGORY_MISMATCH)
                .WithName("CategorySubCategory");
        });

        // 중분류 -> 소분류 매핑 검증
        When(x => x.SubCategory.HasValue && x.DetailCategory.HasValue, () =>
        {
            RuleFor(x => x)
                .Must(x => IsValidSubCategoryDetailCategoryMapping(x.SubCategory!.Value, x.DetailCategory!.Value))
                    .WithErrorCode(ErrorCodes.CATEGORY_SUBCATEGORY_MISMATCH)
                .WithName("SubCategoryDetailCategory");
        });
    }

    private static bool IsValidCategorySubCategoryMapping(GearCategory category, GearSubCategory subCategory)
    {
        return category switch
        {
            GearCategory.Instrument => subCategory is GearSubCategory.Guitar
                or GearSubCategory.Bass
                or GearSubCategory.Drum
                or GearSubCategory.Keyboard
                or GearSubCategory.Wind
                or GearSubCategory.StringInstrument,

            GearCategory.Audio => subCategory is GearSubCategory.Effects
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

    private static bool IsValidSubCategoryDetailCategoryMapping(GearSubCategory subCategory, GearDetailCategory detailCategory)
    {
        return subCategory switch
        {
            GearSubCategory.Guitar => detailCategory is GearDetailCategory.Electric
                or GearDetailCategory.Acoustic
                or GearDetailCategory.Classical,

            GearSubCategory.Bass => detailCategory is GearDetailCategory.ElectricBass
                or GearDetailCategory.AcousticBass
                or GearDetailCategory.UprightBass,

            GearSubCategory.Drum => detailCategory is GearDetailCategory.AcousticDrum
                or GearDetailCategory.ElectronicDrum
                or GearDetailCategory.Percussion,

            GearSubCategory.Keyboard => detailCategory is GearDetailCategory.Piano
                or GearDetailCategory.Synthesizer
                or GearDetailCategory.Midi
                or GearDetailCategory.Organ,

            GearSubCategory.Wind => detailCategory is GearDetailCategory.Saxophone
                or GearDetailCategory.Trumpet
                or GearDetailCategory.Flute
                or GearDetailCategory.Clarinet,

            GearSubCategory.StringInstrument => detailCategory is GearDetailCategory.Violin
                or GearDetailCategory.Viola
                or GearDetailCategory.Cello,

            GearSubCategory.Effects => detailCategory is GearDetailCategory.MultiEffects
                or GearDetailCategory.Pedal
                or GearDetailCategory.BassEffects
                or GearDetailCategory.AcousticEffects
                or GearDetailCategory.Pedalboard
                or GearDetailCategory.PowerSupply
                or GearDetailCategory.EffectsOther,

            GearSubCategory.Mixer => detailCategory is GearDetailCategory.Mixer,

            GearSubCategory.Amp => detailCategory is GearDetailCategory.Amp
                or GearDetailCategory.Preamp
                or GearDetailCategory.PowerAmp,

            GearSubCategory.Speaker => detailCategory is GearDetailCategory.PaSpeaker
                or GearDetailCategory.Subwoofer
                or GearDetailCategory.SpeakerSystem,

            GearSubCategory.Monitor => detailCategory is GearDetailCategory.Monitor
                or GearDetailCategory.StudioMonitor,

            GearSubCategory.AudioInterface => detailCategory is GearDetailCategory.UsbInterface
                or GearDetailCategory.ThunderboltInterface
                or GearDetailCategory.PcieInterface,

            GearSubCategory.Microphone => detailCategory is GearDetailCategory.Condenser
                or GearDetailCategory.Dynamic
                or GearDetailCategory.Ribbon
                or GearDetailCategory.WirelessMic,

            GearSubCategory.Headphone => detailCategory is GearDetailCategory.Headphone
                or GearDetailCategory.Headset,

            GearSubCategory.Iem => detailCategory is GearDetailCategory.Iem,

            GearSubCategory.Earphone => detailCategory is GearDetailCategory.Earphone,

            GearSubCategory.Cable => detailCategory is GearDetailCategory.InstrumentCable
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
