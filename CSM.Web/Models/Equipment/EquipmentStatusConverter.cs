using System.Text.Json;
using System.Text.Json.Serialization;

namespace CSM.Web.Models.Equipment;

public sealed class EquipmentStatusConverter : JsonConverter<string>
{
    public override string Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? "",

            JsonTokenType.Number => reader.GetInt32() switch
            {
                0 => "Available",
                1 => "InUse",
                2 => "Maintenance",
                3 => "Down",
                _ => "Unknown"
            },

            _ => "Unknown"
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        string value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}