namespace CSM.Application.Features.Compliance.QualityChecklists;

public class QualityChecklistResponse
{
    public Guid Id { get; set; }

    public Guid ConstructionSiteId { get; set; }
    public string ConstructionSiteName { get; set; } = string.Empty;

    public Guid CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public List<QualityChecklistItemDto> Items { get; set; } = new();
}