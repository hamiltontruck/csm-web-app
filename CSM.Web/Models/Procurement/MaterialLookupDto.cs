using System.Text.Json.Serialization;

namespace CSM.Web.Models.Procurement;

public class MaterialLookupDto
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}