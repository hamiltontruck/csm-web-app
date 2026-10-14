namespace CSM.Application.Companies.Dtos;

public sealed record CompanyResponse(
    Guid Id,
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
    string BaseCurrencyCode,
    bool IsActive,
    DateTime CreatedAtUtc);