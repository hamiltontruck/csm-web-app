namespace CSM.Application.HRM.Contractors.Dtos;

public sealed class UpdateContractorRequest
{
    public string ContractorCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? ContactPerson { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? TaxIdentificationNumber { get; set; }

    public string? RegistrationNumber { get; set; }

    public string? Address { get; set; }

    public bool IsActive { get; set; }
}
