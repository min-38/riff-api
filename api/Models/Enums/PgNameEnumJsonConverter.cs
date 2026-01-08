using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NpgsqlTypes;

namespace api.Models.Enums;

public class PgNameEnumJsonConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private static readonly Dictionary<string, TEnum> FromLabel = BuildFromLabel();
    private static readonly Dictionary<TEnum, string> ToLabel = BuildToLabel();

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected string for enum {typeof(TEnum).Name}");

        var raw = reader.GetString() ?? string.Empty;
        if (FromLabel.TryGetValue(raw, out var value))
            return value;

        if (Enum.TryParse<TEnum>(raw, ignoreCase: true, out var parsed))
            return parsed;

        throw new JsonException($"Invalid value '{raw}' for enum {typeof(TEnum).Name}");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        if (ToLabel.TryGetValue(value, out var label))
        {
            writer.WriteStringValue(label);
            return;
        }

        writer.WriteStringValue(value.ToString());
    }

    private static Dictionary<string, TEnum> BuildFromLabel()
    {
        var map = new Dictionary<string, TEnum>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in Enum.GetValues<TEnum>())
        {
            var label = GetPgName(value);
            map[label] = value;
            map[value.ToString()] = value;
        }

        return map;
    }

    private static Dictionary<TEnum, string> BuildToLabel()
    {
        var map = new Dictionary<TEnum, string>();
        foreach (var value in Enum.GetValues<TEnum>())
        {
            map[value] = GetPgName(value);
        }

        return map;
    }

    private static string GetPgName(TEnum value)
    {
        var name = value.ToString();
        var field = typeof(TEnum).GetField(name, BindingFlags.Public | BindingFlags.Static);
        var attr = field?.GetCustomAttribute<PgNameAttribute>();
        return string.IsNullOrWhiteSpace(attr?.PgName) ? name : attr!.PgName!;
    }
}
