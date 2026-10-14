using CSM.Domain.Common;
using CSM.Domain.Enums;
using CSM.Domain.Entities.Compliance;




namespace CSM.Domain.Entities.HRM;

public class Employee : TenantEntity
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

    public DateOnly? TerminationDate { get; set; }

    public string? JobTitle { get; set; }

    public string? Department { get; set; }

    public EmployeeType EmployeeType { get; set; }

    public EmployeeScope EmployeeScope { get; set; } = EmployeeScope.Site;

    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    public WageType WageType { get; set; }

    public decimal BaseWage { get; set; }

    public string CurrencyCode { get; set; } = "ETB";

    public string? BankName { get; set; }

    public string? BankAccountNumber { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactPhone { get; set; }

    public ICollection<SafetyIncident> SafetyIncidents { get; set; }
    = new List<SafetyIncident>();

    public ICollection<SiteAssignment> SiteAssignments { get; set; }
        = new List<SiteAssignment>();

    public ICollection<Attendance> Attendances { get; set; }
        = new List<Attendance>();

    public ICollection<LeaveRequest> LeaveRequests { get; set; }
        = new List<LeaveRequest>();

    public ICollection<Timesheet> Timesheets { get; set; }
        = new List<Timesheet>();

    public ICollection<PayrollRecord> PayrollRecords { get; set; }
        = new List<PayrollRecord>();
}
