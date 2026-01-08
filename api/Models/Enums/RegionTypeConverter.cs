using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace api.Models.Enums;

public class RegionTypeConverter : TypeConverter
{
    private static readonly Dictionary<string, Region> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        { "서울", Region.Seoul },
        { "부산", Region.Busan },
        { "대구", Region.Daegu },
        { "인천", Region.Incheon },
        { "광주", Region.Gwangju },
        { "대전", Region.Daejeon },
        { "울산", Region.Ulsan },
        { "세종", Region.Sejong },
        { "경기", Region.Gyeonggi },
        { "강원", Region.Gangwon },
        { "충북", Region.Chungbuk },
        { "충남", Region.Chungnam },
        { "전북", Region.Jeonbuk },
        { "전남", Region.Jeonnam },
        { "경북", Region.Gyeongbuk },
        { "경남", Region.Gyeongnam },
        { "제주", Region.Jeju }
    };

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            if (Map.TryGetValue(text, out var region))
                return region;

            if (Enum.TryParse<Region>(text, ignoreCase: true, out var parsed))
                return parsed;
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is Region region)
        {
            foreach (var pair in Map)
            {
                if (pair.Value == region)
                    return pair.Key;
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
