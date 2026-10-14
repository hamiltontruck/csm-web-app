namespace CSM.Application.Finance.CostCodes.Dtos;

public sealed record UpdateCostCodeRequest(
    string Name,
    string? Description,
    Guid? ParentCostCodeId);
