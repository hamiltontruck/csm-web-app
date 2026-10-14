namespace CSM.Web.Models.HRM;

public sealed class ChangeEmployeeStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public DateOnly? TerminationDate { get; set; }
}