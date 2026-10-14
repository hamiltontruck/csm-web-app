namespace CSM.Application.Companies.Dtos;

public sealed record CreateCompanyRequest(
    string Code,
    string Name,
    string? LegalName,
    string? TaxIdentificationNumber,
    string? RegistrationNumber,
    string? PhoneNumber,
    string? Email,
    string? Address,
    string? City,
    string? Country,
    string BaseCurrencyCode);