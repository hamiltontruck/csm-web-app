using System.Text.Json;
using System.Text.Json.Serialization;

namespace CSM.Web.Models.Procurement;

[JsonConverter(typeof(MaterialTypeConverter))]
public enum MaterialType
{
    Bulk = 0,

    Rebar = 1,

    // API compatibility
    RebarLinear = 1
}

public sealed class MaterialTypeConverter : JsonConverter<MaterialType>
{
    public override MaterialType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return (MaterialType)reader.GetInt32();
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();

            if (Enum.TryParse<MaterialType>(value, true, out var result))
                return result;

            return MaterialType.Bulk;
        }

        return MaterialType.Bulk;
    }

    public override void Write(
        Utf8JsonWriter writer,
        MaterialType value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}