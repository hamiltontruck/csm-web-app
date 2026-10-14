using CSM.Domain.Common;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;
using CSM.Domain.Entities.Identity;

namespace CSM.Domain.Entities.Procurement;

public class MaterialRequest : TenantEntity
{
    public Guid ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }



    public string RequestNumber { get; set; } = string.Empty;

    public DateOnly RequestDate { get; set; }

    public DateOnly? RequiredByDate { get; set; }

    public string? Purpose { get; set; }

    public Priority Priority { get; set; } = Priority.Normal;

    public MaterialRequestStatus Status { get; set; }
        = MaterialRequestStatus.Draft;

    public Guid RequestedBy { get; set; }

    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public string? ApprovalRemarks { get; set; }

    public ConstructionSite ConstructionSite { get; set; } = null!;

    public Project? Project { get; set; }

    public User RequestedByUser { get; set; } = null!;

    public User? ApprovedByUser { get; set; }

    public ICollection<MaterialRequestLine> Lines { get; set; }
        = new List<MaterialRequestLine>();
}