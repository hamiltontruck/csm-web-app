namespace CSM.Web.Models.HRM;

public sealed class CreateTimesheetRequest
{
    public Guid EmployeeId { get; set; }

    public DateOnly PeriodStartDate { get; set; }

    public DateOnly PeriodEndDate { get; set; }

    public string? Remarks { get; set; }
}