namespace CSM.Web.Models.HRM;

public sealed class CreateLeaveRequest
{
    public Guid EmployeeId { get; set; }

    public string LeaveType { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string? Reason { get; set; }
}