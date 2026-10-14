using CSM.Domain.Common;

namespace CSM.Domain.Entities.Compliance;

public sealed class DocumentVersion : BaseEntity
{
    public Guid DocumentId { get; set; }

    public int VersionNumber { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    public string? ContentType { get; set; }

    public long FileSizeBytes { get; set; }

    public string? RevisionNotes { get; set; }

    public bool IsCurrent { get; set; } = true;

    public Guid UploadedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation
    public Document Document { get; set; } = null!;
}