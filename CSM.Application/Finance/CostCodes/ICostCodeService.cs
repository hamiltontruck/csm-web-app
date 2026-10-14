using CSM.Application.Finance.CostCodes.Dtos;

namespace CSM.Application.Finance.CostCodes;

public interface ICostCodeService
{
    Task<IReadOnlyCollection<CostCodeResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CostCodeResponse>> GetActiveAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<CostCodeResponse> GetByIdAsync(
        Guid currentUserId,
        Guid costCodeId,
        CancellationToken cancellationToken = default);

    Task<CostCodeResponse> CreateAsync(
        Guid currentUserId,
        CreateCostCodeRequest request,
        CancellationToken cancellationToken = default);

    Task<CostCodeResponse> UpdateAsync(
        Guid currentUserId,
        Guid costCodeId,
        UpdateCostCodeRequest request,
        CancellationToken cancellationToken = default);

    Task<CostCodeResponse> ChangeActiveStatusAsync(
        Guid currentUserId,
        Guid costCodeId,
        ChangeCostCodeActiveStatusRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid currentUserId,
        Guid costCodeId,
        CancellationToken cancellationToken = default);

    Task<List<CostCodeResponse>> GetBySiteAsync(
         Guid siteId,
         CancellationToken cancellationToken = default);


}
