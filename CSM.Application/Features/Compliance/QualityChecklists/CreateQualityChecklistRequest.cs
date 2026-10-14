using System.ComponentModel.DataAnnotations;

namespace CSM.Application.Features.Compliance.QualityChecklists;

public class CreateQualityChecklistRequest
{
    [Required]
    public Guid ConstructionSiteId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<CreateQualityChecklistItemRequest> Items { get; set; } = new();
}

public class CreateQualityChecklistItemRequest
{
    public int Sequence { get; set; }

    [Required, MaxLength(500)]
    public string Requirement { get; set; } = string.Empty;

    public bool IsMandatory { get; set; } = true;
}