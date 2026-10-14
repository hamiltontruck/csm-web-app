using CSM.Application.Finance.PettyCashAccounts.Dtos;

namespace CSM.Application.Finance.PettyCashAccounts;

public interface IPettyCashAccountService
{
    Task<IReadOnlyCollection<PettyCashAccountResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PettyCashAccountResponse>> GetSiteAccountsAsync(
        Guid currentUserId,
        Guid siteId,
        CancellationToken cancellationToken = default);

    Task<PettyCashAccountResponse> GetByIdAsync(
        Guid currentUserId,
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<PettyCashAccountResponse> CreateAsync(
        Guid currentUserId,
        CreatePettyCashAccountRequest request,
        CancellationToken cancellationToken = default);

    Task<PettyCashAccountResponse> UpdateAsync(
        Guid currentUserId,
        Guid accountId,
        UpdatePettyCashAccountRequest request,
        CancellationToken cancellationToken = default);
}