using CSM.Domain.Common;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;

namespace CSM.Domain.Entities.Compliance;

public class NonConformanceReport : TenantEntity
{
    public Guid ConstructionSiteId { get; set; }
    public ConstructionSite ConstructionSite { get; set; } = null!;

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid RaisedById { get; set; }
    public User RaisedBy { get; set; } = null!;

    public Guid? AssignedToEmployeeId { get; set; }
    public Employee? AssignedToEmployee { get; set; }

    public string NcrNumber { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public NcrSeverity Severity { get; set; } = NcrSeverity.Minor;

    public NcrStatus Status { get; set; } = NcrStatus.Open;

    public DateTime ReportedDate { get; set; }

    public DateTime? DueDate { get; set; }

    public DateTime? ClosedDate { get; set; }

    public string? RootCause { get; set; }

    public string? CorrectiveAction { get; set; }

    public string? PreventiveAction { get; set; }
}