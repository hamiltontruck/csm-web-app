namespace CSM.Web.Models.HRM;

public sealed class TimesheetResponse
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }

    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;

    public DateOnly PeriodStartDate { get; set; }
    public DateOnly PeriodEndDate { get; set; }

    public decimal RegularHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal TotalHours { get; set; }

    public string Status { get; set; } = string.Empty;

    public Guid? SubmittedBy { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }

    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    public string? ApprovalRemarks { get; set; }
    public string? Remarks { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}