namespace CSM.Application.Finance.BudgetLines.Dtos;

public sealed record UpdateBudgetLineRequest(
    Guid CostCodeId,
    string? Description,
    decimal BudgetedAmount,
    decimal RevisedAmount);
