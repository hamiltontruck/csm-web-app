using CSM.Domain.Enums;

namespace CSM.Application.HRM.Employees.Dtos;

public sealed record CreateEmployeeRequest(
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
    string? JobTitle,
    string? Department,
    EmployeeType EmployeeType,
    EmployeeScope EmployeeScope,
    WageType WageType,
    decimal BaseWage,
    string CurrencyCode,
    string? BankName,
    string? BankAccountNumber,
    string? EmergencyContactName,
    string? EmergencyContactPhone);