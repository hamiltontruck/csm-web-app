namespace CSM.Web.Models.HRM;

public sealed class CreateEmployeeRequest
{
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? NationalIdNumber { get; set; }
    public string? TaxIdentificationNumber { get; set; }

    public DateOnly? DateOfBirth { get; set; }
    public DateOnly HireDate { get; set; }

    public string? JobTitle { get; set; }
    public string? Department { get; set; }

    public string EmployeeType { get; set; } = string.Empty;
    public string EmployeeScope { get; set; } = "Site";
    public string WageType { get; set; } = string.Empty;

    public decimal BaseWage { get; set; }
    public string CurrencyCode { get; set; } = "ETB";

    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }

    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
}