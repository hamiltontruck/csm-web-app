using Microsoft.AspNetCore.Http;

namespace CSM.Api.Models.Documents;

public class UploadDocumentVersionRequest
{
    public IFormFile File { get; set; } = default!;
    public string? RevisionNotes { get; set; }
}