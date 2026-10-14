using CSM.Domain.Enums;

namespace CSM.Application.Compliance.Permits.Dtos;

public sealed class UpdatePermitRequest
{
    public Guid ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public string PermitNumber { get; set; } = string.Empty;

    public string PermitType { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? IssuingAuthority { get; set; }

    public DateOnly? ApplicationDate { get; set; }

    public DateOnly? IssueDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public PermitStatus Status { get; set; }

    public int ExpiryAlertDays { get; set; }

    public string? Conditions { get; set; }

    public string? Notes { get; set; }
}
