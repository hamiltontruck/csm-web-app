namespace CSM.Web.Models.Compliance;

public sealed class CreateDocumentRequest
{
    public string DocumentNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public int DocumentType { get; set; }

    public string? DrawingNumber { get; set; }
    public string? SheetNumber { get; set; }
    public string? Discipline { get; set; }
    public string? DrawingStatus { get; set; }
    public string? CurrentRevision { get; set; }

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