using CSM.Domain.Enums;

namespace CSM.Application.Finance.Budgets.Dtos;

public sealed record ChangeBudgetStatusRequest(
    BudgetStatus Status);
