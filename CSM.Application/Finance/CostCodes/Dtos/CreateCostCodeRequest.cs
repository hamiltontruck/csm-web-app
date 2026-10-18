namespace CSM.Application.Finance.CostCodes.Dtos;

public sealed record CreateCostCodeRequest(
    string Code,
    string Name,
    string? Description,
    Guid? ParentCostCodeId,
    Guid? ConstructionSiteId,
    Guid? CompanyId = null);
