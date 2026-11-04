using CSM.Application.Finance.PettyCashTransactions.Dtos;

namespace CSM.Application.Finance.PettyCashTransactions;

public interface IPettyCashTransactionService
{
    Task<IReadOnlyCollection<PettyCashCostCodeOption>> GetCostCodeOptionsAsync(
        Guid currentUserId, Guid pettyCashAccountId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PettyCashTransactionResponse>> GetAllAsync(
        Guid currentUserId,
        Guid pettyCashAccountId,
        CancellationToken cancellationToken = default);

    Task<PettyCashTransactionResponse> CreateAsync(
        Guid currentUserId,
        CreatePettyCashTransactionRequest request,
        CancellationToken cancellationToken = default);
}