using System.Text.Json;
using System.Text.Json.Serialization;

namespace SleepTokenWatcher.State;

/// <summary>
/// Reads an id written as a JSON number (state files from before ids became strings) or a string.
/// Without it, an upgrade would discard the saved state and silently re-baseline.
/// </summary>
internal sealed class NumberOrStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            _ => throw new JsonException($"Expected a number or string id, got {reader.TokenType}."),
        };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
