using CSM.Application.Finance.Expenses.Dtos;
namespace CSM.Application.Finance.Expenses;
public interface IExpenseSettlementService
{
    Task<IReadOnlyList<ExpenseSettlementOption>> GetOptionsAsync(Guid userId, string purpose, Guid? siteId, Guid? accountId, CancellationToken ct = default);
    Task<ExpenseSettlementOptionsResponse> GetForExpenseAsync(Guid userId, Guid expenseId, string purpose, CancellationToken ct = default);
}
