using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradeLedger.Core.Integrations.Bitunix;

public sealed class BitunixDecimalConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        BitunixJson.ReadDecimal(ref reader) ?? 0m;

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
}

public sealed class BitunixNullableDecimalConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        BitunixJson.ReadDecimal(ref reader);

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value.Value.ToString(CultureInfo.InvariantCulture));
        }
    }
}

public sealed class BitunixNullableInt64Converter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number => reader.GetInt64(),
            JsonTokenType.String when long.TryParse(
                reader.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) => v,
            _ => null,
        };

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteNumberValue(value.Value);
        }
    }
}

public static class BitunixJson
{
    private const NumberStyles DecimalStyles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    public static readonly JsonSerializerOptions Options = CreateOptions();

    public static string CompactBody(object? body) =>
        body is null ? string.Empty : JsonSerializer.Serialize(body, Options);

    internal static decimal? ReadDecimal(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.Number:
                return reader.GetDecimal();

            case JsonTokenType.String:
            {
                var raw = reader.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return null;
                }

                return decimal.TryParse(raw, DecimalStyles, CultureInfo.InvariantCulture, out var value)
                    ? value
                    : null;
            }

            default:
                return null;
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

            WriteIndented = false,
        };

        options.Converters.Add(new BitunixDecimalConverter());
        options.Converters.Add(new BitunixNullableDecimalConverter());
        options.Converters.Add(new BitunixNullableInt64Converter());
        return options;
    }
}
