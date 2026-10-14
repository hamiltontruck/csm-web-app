namespace CSM.Application.Finance.Budgets.Dtos;

public sealed record UpdateBudgetRequest(
    string Name,
    string? Description,
    decimal TotalAmount,
    string CurrencyCode,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo);
