using System.Text.Json.Serialization;

namespace CSM.Web.Models.Procurement;

public class VendorResponse
{
    public Guid Id { get; set; }

    public string VendorCode { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    // Backward compatibility for existing Razor pages
    [JsonIgnore]
    public string VendorName => Name;

    public string ContactPerson { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Email { get; set; } = "";

    public string TaxNumber { get; set; } = "";

    public string BankName { get; set; } = "";
    public string BankAccountNumber { get; set; } = "";

    public string Address { get; set; } = "";

    public bool IsActive { get; set; }
}