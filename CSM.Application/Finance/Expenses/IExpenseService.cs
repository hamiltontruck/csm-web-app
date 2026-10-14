using CSM.Application.Finance.Expenses.Dtos;

namespace CSM.Application.Finance.Expenses;

public interface IExpenseService
{
    Task<IReadOnlyCollection<ExpenseResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ExpenseResponse>> GetBySiteAsync(
        Guid currentUserId,
        Guid constructionSiteId,
        CancellationToken cancellationToken = default);

    Task<ExpenseResponse> GetByIdAsync(
        Guid currentUserId,
        Guid expenseId,
        CancellationToken cancellationToken = default);

    Task<ExpenseResponse> CreateAsync(
        Guid currentUserId,
        CreateExpenseRequest request,
        CancellationToken cancellationToken = default);

    Task<ExpenseResponse> UpdateAsync(
        Guid currentUserId,
        Guid expenseId,
        UpdateExpenseRequest request,
        CancellationToken cancellationToken = default);

    Task<ExpenseResponse> ChangeStatusAsync(
        Guid currentUserId,
        Guid expenseId,
        ChangeExpenseStatusRequest request,
        CancellationToken cancellationToken = default);
}
