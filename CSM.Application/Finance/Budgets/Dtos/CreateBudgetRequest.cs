using CSM.Domain.Enums;

namespace CSM.Application.Finance.Budgets.Dtos;

public sealed record CreateBudgetRequest(
    Guid? ConstructionSiteId,
    Guid? ProjectId,
    string BudgetNumber,
    string Name,
    string? Description,
    decimal TotalAmount,
    string CurrencyCode,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo);
