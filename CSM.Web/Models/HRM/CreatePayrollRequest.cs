namespace CSM.Web.Models.HRM;

public sealed class CreatePayrollRequest
{
    public Guid EmployeeId { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }
}