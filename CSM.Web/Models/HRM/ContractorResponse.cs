namespace CSM.Web.Models.HRM;

public sealed class ContractorResponse
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }

    public string ContractorCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public string? ContactPerson { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }

    public string? TaxIdentificationNumber { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? Address { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}