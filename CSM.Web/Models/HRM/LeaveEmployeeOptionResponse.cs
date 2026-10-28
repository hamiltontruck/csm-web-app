namespace CSM.Web.Models.HRM;

public sealed class LeaveEmployeeOptionResponse
{
    public Guid Id { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
