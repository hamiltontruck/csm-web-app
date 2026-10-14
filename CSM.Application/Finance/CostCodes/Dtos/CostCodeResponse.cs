namespace CSM.Application.Finance.CostCodes.Dtos;

public sealed record CostCodeResponse(
    Guid Id,
    Guid CompanyId,
    Guid? ConstructionSiteId,
    string? ParentCode,
    string? ParentName,
    string Code,
    string Name,
    string? Description,
    Guid? ParentCostCodeId,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
