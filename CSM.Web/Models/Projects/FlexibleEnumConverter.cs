using System.Text.Json;
using System.Text.Json.Serialization;

namespace CSM.Web.Models.Projects;

public sealed class FlexibleMilestoneStatusConverter
    : JsonConverter<MilestoneStatus>
{
    public override MilestoneStatus Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return (MilestoneStatus)reader.GetInt32();
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString()!;

            if (int.TryParse(value, out var number))
                return (MilestoneStatus)number;

            return Enum.Parse<MilestoneStatus>(value, true);
        }

        throw new JsonException("Invalid milestone status.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        MilestoneStatus value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}