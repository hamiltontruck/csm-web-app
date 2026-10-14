using CSM.Domain.Enums;

namespace CSM.Application.Compliance.Documents.Dtos;

public sealed class DocumentResponse
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public string? ConstructionSiteName { get; init; }

    public string DocumentNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DocumentType DocumentType { get; set; }

    public string? DrawingNumber { get; init; }

    public string? SheetNumber { get; init; }

    public DrawingDiscipline? Discipline { get; init; }

    public DrawingStatus? DrawingStatus { get; init; }

    public string? CurrentRevision { get; init; }

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

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
