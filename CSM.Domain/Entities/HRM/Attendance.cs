using CSM.Domain.Common;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;

namespace CSM.Domain.Entities.HRM;

public class Attendance : TenantEntity
{
    public Guid EmployeeId { get; set; }

    public Guid ConstructionSiteId { get; set; }

    public Guid? ShiftId { get; set; }

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

    public AttendanceStatus Status { get; set; }

    public AttendanceSource Source { get; set; }

    public decimal RegularHours { get; set; }

    public decimal OvertimeHours { get; set; }

    public string? BiometricReference { get; set; }

    public bool RequiresApproval { get; set; }

    public bool IsApproved { get; set; }

    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public string? ManualOverrideReason { get; set; }

    public string? Remarks { get; set; }

    public Employee Employee { get; set; } = null!;

    public ConstructionSite ConstructionSite { get; set; } = null!;

    public Shift? Shift { get; set; }
}