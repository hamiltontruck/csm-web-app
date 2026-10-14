using CSM.Domain.Common;

namespace CSM.Domain.Entities.Projects;

public sealed class EquipmentAssignment : BaseEntity
{
    public Guid EquipmentId { get; set; }

    public Guid ConstructionSiteId { get; set; }

    public Guid ProjectId { get; set; }

    public DateOnly AssignedDate { get; set; }

    public DateOnly? ReleasedDate { get; set; }

    public string? OperatorName { get; set; }

    public string? Remarks { get; set; }

    public Equipment Equipment { get; set; } = null!;
}