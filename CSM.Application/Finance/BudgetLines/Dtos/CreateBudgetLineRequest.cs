namespace CSM.Application.Finance.BudgetLines.Dtos;

public sealed record CreateBudgetLineRequest(
    Guid BudgetId,
    Guid CostCodeId,
    string? Description,
    decimal BudgetedAmount,
    decimal RevisedAmount);
