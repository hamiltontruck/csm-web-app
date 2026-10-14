using System.ComponentModel.DataAnnotations;

namespace CSM.Application.CompanySettings.Dtos;

public sealed class UpdateCompanySettingsRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string ShortName { get; set; } = string.Empty;

    public string RegistrationNumber { get; set; } =
        string.Empty;

    public string TaxNumber { get; set; } =
        string.Empty;

    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Website { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string Currency { get; set; } = "ETB";

    public string TimeZone { get; set; } =
        "Africa/Addis_Ababa";

    [Range(1, 12)]
    public int FiscalYearStartMonth { get; set; } = 1;

    public string WorkStartTime { get; set; } = "08:00";
    public string WorkEndTime { get; set; } = "17:00";
    public int LunchBreakMinutes { get; set; }

    public int LateGraceMinutes { get; set; }
    public decimal OvertimeAfterHours { get; set; }

    public bool NotifyLowInventory { get; set; }
    public bool NotifyMaintenance { get; set; }
    public bool NotifyPermitExpiry { get; set; }
    public bool NotifyDailyProgress { get; set; }

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