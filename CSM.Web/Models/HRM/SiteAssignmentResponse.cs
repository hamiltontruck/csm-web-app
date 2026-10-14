namespace CSM.Web.Models.HRM;

public sealed class SiteAssignmentResponse
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Guid EmployeeId { get; set; }

    public string EmployeeNumber { get; set; } = string.Empty;

    public string EmployeeName { get; set; } = string.Empty;

    public Guid ConstructionSiteId { get; set; }

    public string SiteName { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? RoleAtSite { get; set; }

    public bool IsPrimaryAssignment { get; set; }

    public string? Remarks { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}