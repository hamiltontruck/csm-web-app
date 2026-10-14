using CSM.Domain.Common;

namespace CSM.Domain.Entities.Organization;

public class Company : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? LegalName { get; set; }

    public string? ShortName { get; set; }

    public string? TaxNumber { get; set; }

    public string? Phone { get; set; }

    public string? Website { get; set; }

    public string Currency { get; set; } = "ETB";

    public string TimeZone { get; set; } = "Africa/Addis_Ababa";

    public int FiscalYearStartMonth { get; set; } = 1;

    public string? TaxIdentificationNumber { get; set; }

    public string? RegistrationNumber { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }

    public string BaseCurrencyCode { get; set; } = "ETB";

    public bool IsActive { get; set; } = true;

    public ICollection<BusinessUnit> BusinessUnits { get; set; }
        = new List<BusinessUnit>();

    public ICollection<ConstructionSite> ConstructionSites { get; set; }
        = new List<ConstructionSite>();

    // Business Hours
    public string WorkStartTime { get; set; } = "08:00";

    public string WorkEndTime { get; set; } = "17:00";

    public int LunchBreakMinutes { get; set; } = 60;

    // Attendance Rules
    public int LateGraceMinutes { get; set; } = 15;

    public decimal OvertimeAfterHours { get; set; } = 8m;

    // Notifications
    public bool NotifyLowInventory { get; set; } = true;

    public bool NotifyMaintenance { get; set; } = true;

    public bool NotifyPermitExpiry { get; set; } = true;

    public bool NotifyDailyProgress { get; set; } = true;

    // Weekend Configuration
    public string WeekendDays { get; set; } = "Saturday,Sunday";

    public string? LogoUrl { get; set; }

    public string PrimaryColor { get; set; } = "#156082";
    public string SecondaryColor { get; set; } = "#E6F4F1";

    public string DefaultLanguage { get; set; } = "en";
    public string DateFormat { get; set; } = "dd/MM/yyyy";
    public string NumberFormat { get; set; } = "en-ET";
    public string MeasurementUnit { get; set; } = "Metric";

    public string ProjectPrefix { get; set; } = "PRJ";
    public string PurchaseOrderPrefix { get; set; } = "PO";
    public string StockIssuePrefix { get; set; } = "SI";
    public string EmployeePrefix { get; set; } = "EMP";
}