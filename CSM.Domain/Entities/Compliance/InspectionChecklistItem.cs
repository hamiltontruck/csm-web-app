using CSM.Domain.Common;

namespace CSM.Domain.Entities.Compliance;

public class InspectionChecklistItem : TenantEntity
{
    public Guid InspectionId { get; set; }

    public int Sequence { get; set; }

    public string Requirement { get; set; } = string.Empty;

    public bool? IsCompliant { get; set; }

    public string? Observation { get; set; }

    public string? CorrectiveAction { get; set; }

    public DateOnly? DueDate { get; set; }

    public bool IsResolved { get; set; }

    public Inspection Inspection { get; set; } = null!;
}