using CSM.Domain.Enums;

namespace CSM.Application.HRM.Employees.Dtos;

public sealed record EmployeeResponse(
    Guid Id,
    Guid CompanyId,
    string EmployeeNumber,
    string FirstName,
    string? MiddleName,
    string LastName,
    string? PhoneNumber,
    string? Email,
    string? NationalIdNumber,
    string? TaxIdentificationNumber,
    DateOnly? DateOfBirth,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    string? JobTitle,
    string? Department,
    EmployeeType EmployeeType,
    EmployeeScope EmployeeScope,
    EmployeeStatus Status,
    WageType WageType,
    decimal BaseWage,
    string CurrencyCode,
    string? BankName,
    string? BankAccountNumber,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    DateTime CreatedAtUtc);