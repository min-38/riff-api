using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace api.Models.Enums;

public class GearConditionTypeConverter : TypeConverter
{
    private static readonly Dictionary<string, GearCondition> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        { "new", GearCondition.New },
        { "like_new", GearCondition.LikeNew },
        { "good", GearCondition.Good },
        { "fair", GearCondition.Fair }
    };

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            if (Map.TryGetValue(text, out var condition))
                return condition;

            if (Enum.TryParse<GearCondition>(text, ignoreCase: true, out var parsed))
                return parsed;
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is GearCondition condition)
        {
            foreach (var pair in Map)
            {
                if (pair.Value == condition)
                    return pair.Key;
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
