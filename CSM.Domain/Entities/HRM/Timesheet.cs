using CSM.Domain.Common;
using CSM.Domain.Enums;

namespace CSM.Domain.Entities.HRM;

public class Timesheet : TenantEntity
{
    public Guid EmployeeId { get; set; }

    public DateOnly PeriodStartDate { get; set; }

    public DateOnly PeriodEndDate { get; set; }

    public decimal RegularHours { get; set; }

    public decimal OvertimeHours { get; set; }

    public decimal TotalHours { get; set; }

    public TimesheetStatus Status { get; set; } = TimesheetStatus.Draft;

    public Guid? SubmittedBy { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public string? ApprovalRemarks { get; set; }

    public string? Remarks { get; set; }

    public Employee Employee { get; set; } = null!;
}

