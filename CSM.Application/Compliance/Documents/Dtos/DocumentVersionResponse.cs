namespace CSM.Application.Compliance.Documents.Dtos;

public sealed class DocumentVersionResponse
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public int VersionNumber { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    public string? ContentType { get; set; }

    public long FileSizeBytes { get; set; }

    public string? RevisionNotes { get; set; }

    public bool IsCurrent { get; set; }

    public Guid UploadedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}