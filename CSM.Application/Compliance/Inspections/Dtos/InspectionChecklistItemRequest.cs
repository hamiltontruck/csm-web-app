namespace CSM.Application.Compliance.Inspections.Dtos;

public sealed class InspectionChecklistItemRequest
{
    public int Sequence { get; set; }

    public string Requirement { get; set; } = string.Empty;

    public bool? IsCompliant { get; set; }

    public string? Observation { get; set; }

    public string? CorrectiveAction { get; set; }

    public DateOnly? DueDate { get; set; }

    public bool IsResolved { get; set; }
}
