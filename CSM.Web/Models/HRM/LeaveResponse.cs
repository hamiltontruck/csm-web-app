namespace CSM.Web.Models.HRM;

public sealed class LeaveResponse
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }

    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;

    public string LeaveType { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public decimal NumberOfDays { get; set; }

    public string? Reason { get; set; }

    public string Status { get; set; } = string.Empty;

    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }

    public string? ReviewRemarks { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}