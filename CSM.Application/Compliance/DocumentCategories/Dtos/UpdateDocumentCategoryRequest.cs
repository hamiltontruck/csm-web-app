namespace CSM.Application.Compliance.DocumentCategories.Dtos;

public sealed class UpdateDocumentCategoryRequest
{
    public string CategoryCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }
}
