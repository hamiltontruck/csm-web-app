namespace CSM.Web.Models.HRM;

public sealed class UpdateSiteAssignmentRequest
{
    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? RoleAtSite { get; set; }

    public bool IsPrimaryAssignment { get; set; }

    public string? Remarks { get; set; }
}