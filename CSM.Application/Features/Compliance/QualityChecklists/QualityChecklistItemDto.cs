namespace CSM.Application.Features.Compliance.QualityChecklists;

public class QualityChecklistItemDto
{
    public Guid Id { get; set; }

    public int Sequence { get; set; }

    public string Requirement { get; set; } = string.Empty;

    public bool IsMandatory { get; set; }
}