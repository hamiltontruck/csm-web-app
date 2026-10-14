using CSM.Domain.Enums;

namespace CSM.Application.Finance.Expenses.Dtos;

public sealed record ChangeExpenseStatusRequest(
    ExpenseStatus Status);
