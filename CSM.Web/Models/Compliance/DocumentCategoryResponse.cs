namespace CSM.Web.Models.Compliance;

public sealed class DocumentCategoryResponse
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public string CategoryCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}