namespace CSM.Web.Models.HRM;

public sealed class AttendanceResponse
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }

    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;

    public Guid ConstructionSiteId { get; set; }
    public string SiteName { get; set; } = string.Empty;

    public Guid? ShiftId { get; set; }
    public string? ShiftCode { get; set; }
    public string? ShiftName { get; set; }

    public DateOnly AttendanceDate { get; set; }

    public DateTime? CheckInAtUtc { get; set; }
    public DateTime? CheckOutAtUtc { get; set; }

    public decimal? CheckInLatitude { get; set; }
    public decimal? CheckInLongitude { get; set; }

    public decimal? CheckOutLatitude { get; set; }
    public decimal? CheckOutLongitude { get; set; }

    public decimal? CheckInAccuracyMeters { get; set; }
    public decimal? CheckOutAccuracyMeters { get; set; }

    public bool IsCheckInWithinGeofence { get; set; }
    public bool IsCheckOutWithinGeofence { get; set; }

    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;

    public decimal RegularHours { get; set; }
    public decimal OvertimeHours { get; set; }

    public string? BiometricReference { get; set; }

    public bool RequiresApproval { get; set; }
    public bool IsApproved { get; set; }

    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    public string? ManualOverrideReason { get; set; }
    public string? Remarks { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}