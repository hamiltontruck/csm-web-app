namespace CSM.Web.Models.Procurement;

public class SupplierResponse
{
    public Guid Id { get; set; }

    public string VendorCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? ContactPerson { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public bool IsActive { get; set; }
}