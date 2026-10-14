using CSM.Domain.Enums;

namespace CSM.Application.Procurement.MaterialRequests.Dtos;

public sealed class CreateMaterialRequestRequest
{
    public Guid ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public string RequestNumber { get; set; } = string.Empty;

    public DateOnly RequestDate { get; set; }

    public DateOnly? RequiredByDate { get; set; }

    public string? Purpose { get; set; }

    public Priority Priority { get; set; } = Priority.Normal;

    public IReadOnlyCollection<MaterialRequestLineInput> Lines { get; set; }
        = Array.Empty<MaterialRequestLineInput>();
}
