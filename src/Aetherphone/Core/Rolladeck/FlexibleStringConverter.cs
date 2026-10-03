using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aetherphone.Core.Rolladeck;

internal sealed class FlexibleStringConverter : JsonConverter<string?>
{
    public override bool HandleNull => true;

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => System.Text.Encoding.UTF8.GetString(reader.ValueSpan),
            JsonTokenType.Null => null,
            _ => SkipUnknown(ref reader),
        };
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value);
    }

    private static string? SkipUnknown(ref Utf8JsonReader reader)
    {
        reader.Skip();
        return null;
    }
}
