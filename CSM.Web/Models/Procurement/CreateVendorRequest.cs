namespace CSM.Web.Models.Procurement;

public class CreateVendorRequest
{
    public Guid? CompanyId { get; set; }

    public string VendorCode { get; set; } = "";

    public string Name { get; set; } = "";

    public string? ContactPerson { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }

    public string? TaxIdentificationNumber { get; set; }
    public string? RegistrationNumber { get; set; }

    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;
}