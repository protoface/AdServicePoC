using System.Text.Json;
using System.Text.Json.Serialization;

namespace MediAdIdentityPoC;

public class StrictEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.GetInt32().ToString(),
            _ => throw new JsonException($"Invalid token type, expected {JsonTokenType.String} or {JsonTokenType.Number}, was {reader.TokenType}")
        };
        return Enum.TryParse(value, true, out TEnum result) && Enum.IsDefined(result)
            ? result
            : throw new JsonException($"Failed to parse payload \"{value}\" to enum of type \"{typeof(TEnum).Name}\"");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}