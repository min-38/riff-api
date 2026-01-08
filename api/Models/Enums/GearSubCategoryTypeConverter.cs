using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace api.Models.Enums;

public class GearSubCategoryTypeConverter : TypeConverter
{
    private static readonly Dictionary<string, GearSubCategory> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        { "guitar", GearSubCategory.Guitar },
        { "bass", GearSubCategory.Bass },
        { "drum", GearSubCategory.Drum },
        { "keyboard", GearSubCategory.Keyboard },
        { "wind", GearSubCategory.Wind },
        { "string_instrument", GearSubCategory.StringInstrument },
        { "effects", GearSubCategory.Effects },
        { "mixer", GearSubCategory.Mixer },
        { "amp", GearSubCategory.Amp },
        { "speaker", GearSubCategory.Speaker },
        { "monitor", GearSubCategory.Monitor },
        { "audio_interface", GearSubCategory.AudioInterface },
        { "microphone", GearSubCategory.Microphone },
        { "headphone", GearSubCategory.Headphone },
        { "iem", GearSubCategory.Iem },
        { "earphone", GearSubCategory.Earphone },
        { "cable", GearSubCategory.Cable },
        { "stand", GearSubCategory.Stand },
        { "case", GearSubCategory.Case },
        { "pick", GearSubCategory.Pick },
        { "string_accessory", GearSubCategory.StringAccessory },
        { "drumstick", GearSubCategory.Drumstick },
        { "other", GearSubCategory.Other }
    };

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            if (Map.TryGetValue(text, out var subCategory))
                return subCategory;

            if (Enum.TryParse<GearSubCategory>(text, ignoreCase: true, out var parsed))
                return parsed;
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is GearSubCategory subCategory)
        {
            foreach (var pair in Map)
            {
                if (pair.Value == subCategory)
                    return pair.Key;
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
