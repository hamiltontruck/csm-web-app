using CSM.Domain.Common;
using CSM.Domain.Enums;
using CSM.Domain.Entities.Compliance;

namespace CSM.Domain.Entities.Compliance;

public class Document : TenantEntity
{
    public string DocumentNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DocumentType DocumentType { get; set; }

    public string? DrawingNumber { get; set; }

    public string? SheetNumber { get; set; }

    public DrawingDiscipline? Discipline { get; set; }

    public DrawingStatus? DrawingStatus { get; set; }

    public string? CurrentRevision { get; set; }

    public ICollection<DocumentVersion> Versions { get; set; }
        = new List<DocumentVersion>();

    public string FileName { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    public string? ContentType { get; set; }

    public long FileSizeBytes { get; set; }

    public Guid? ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? EmployeeId { get; set; }

    public Guid? RelatedEntityId { get; set; }

    public string? RelatedEntityType { get; set; }

    public DateOnly? IssueDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public bool IsConfidential { get; set; }

    public string? Description { get; set; }
}