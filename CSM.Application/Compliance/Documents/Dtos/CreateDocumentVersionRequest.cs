namespace CSM.Application.Compliance.Documents.Dtos;

public sealed class CreateDocumentVersionRequest
{
    public string FileName { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    public string? ContentType { get; set; }

    public long FileSizeBytes { get; set; }

    public string? RevisionNotes { get; set; }
}