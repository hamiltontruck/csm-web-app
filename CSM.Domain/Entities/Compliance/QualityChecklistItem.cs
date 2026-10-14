using CSM.Domain.Common;

namespace CSM.Domain.Entities.Compliance;

public class QualityChecklistItem : AuditableEntity
{
    public Guid QualityChecklistId { get; set; }

    public QualityChecklist QualityChecklist { get; set; } = null!;

    public int Sequence { get; set; }

    public string Requirement { get; set; } = string.Empty;

    public bool IsMandatory { get; set; } = true;
}