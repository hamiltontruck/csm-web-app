namespace CSM.Application.Finance.BudgetLines.Dtos;

public sealed record BudgetLineResponse(
    Guid Id,
    Guid CompanyId,
    Guid BudgetId,
    Guid CostCodeId,
    string CostCode,
    string? Description,
    decimal BudgetedAmount,
    decimal RevisedAmount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
