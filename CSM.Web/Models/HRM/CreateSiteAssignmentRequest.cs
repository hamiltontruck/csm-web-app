namespace CSM.Web.Models.HRM;

public sealed class CreateSiteAssignmentRequest
{
    public Guid EmployeeId { get; set; }

    public Guid ConstructionSiteId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? RoleAtSite { get; set; }

    public bool IsPrimaryAssignment { get; set; }

    public string? Remarks { get; set; }
}