using CSM.Domain.Common;
using CSM.Domain.Enums;

namespace CSM.Domain.Entities.HRM;

public class LeaveRequest : TenantEntity
{
    public Guid EmployeeId { get; set; }

    public string LeaveType { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public decimal NumberOfDays { get; set; }

    public string? Reason { get; set; }

    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

    public Guid? ReviewedBy { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public string? ReviewRemarks { get; set; }

    public Employee Employee { get; set; } = null!;
}