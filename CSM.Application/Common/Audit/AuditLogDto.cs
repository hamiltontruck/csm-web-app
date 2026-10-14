namespace CSM.Application.Common.Audit;

public sealed class AuditLogDto
{
    public Guid Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}