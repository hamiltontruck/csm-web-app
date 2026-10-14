using CSM.Application.HRM.Contractors.Dtos;

namespace CSM.Application.HRM.Contractors;

public interface IContractorService
{
    Task<IReadOnlyCollection<ContractorResponse>> GetAllAsync(
        Guid currentUserId,
        bool? isActive,
        CancellationToken cancellationToken = default);

    Task<ContractorResponse> GetByIdAsync(
        Guid currentUserId,
        Guid contractorId,
        CancellationToken cancellationToken = default);

    Task<ContractorResponse> CreateAsync(
        Guid currentUserId,
        CreateContractorRequest request,
        CancellationToken cancellationToken = default);

    Task<ContractorResponse> UpdateAsync(
        Guid currentUserId,
        Guid contractorId,
        UpdateContractorRequest request,
        CancellationToken cancellationToken = default);
}
